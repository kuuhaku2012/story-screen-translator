"""Lightweight reciprocal line-neighbor grouping; no manga-specific coordinates or vocabulary."""
import math

def group_lines(blocks):
    lines=[]
    for block in blocks:
        if block['text'].strip().isdigit():
            continue
        points=block['box']
        box=[min(p[0] for p in points),min(p[1] for p in points),max(p[0] for p in points),max(p[1] for p in points)]
        if box[2]<=box[0] or box[3]<=box[1]: continue
        lines.append({'text':block['text'],'box':box})
    edges=[]
    for i,a in enumerate(lines):
        ax,ay,ar,ab=a['box']; ah=ab-ay; aw=ar-ax
        for j,b in enumerate(lines):
            if i==j: continue
            bx,by,br,bb=b['box']; bh=bb-by; bw=br-bx
            height=max(ah,bh)
            dy=(by+bb-ay-ab)/2
            dx=abs((bx+br-ax-ar)/2)
            gap=by-ab
            overlap=max(0,min(ar,br)-max(ax,bx))
            # A wide heading can align with a small dialogue below it.
            # Reject the scale jump before chaining; otherwise its union covers neighboring bubbles.
            wider_aspect=aw/ah if aw>=bw else bw/bh
            if max(aw,bw)>min(aw,bw)*3 and wider_aspect>6: continue
            if dy<height*.4 or gap < -height*.5 or gap>height*1.05: continue
            if min(ah,bh)<height*.45: continue
            if dx>max(height*.9,min(aw,bw)*.55) and not (overlap>=min(aw,bw)*.65 and dx<=max(aw,bw)*.48): continue
            score=dx/height+max(gap,0)/height+dy/height*.5+abs(math.log(aw/bw))*.15
            edges.append((score,i,j))
    outgoing={};incoming={}
    for score,i,j in sorted(edges):
        if i not in outgoing: outgoing[i]=(score,j)
        if j not in incoming: incoming[j]=(score,i)
    successors={i:j for i,(_,j) in outgoing.items() if incoming.get(j,(None,None))[1]==i}
    previous=set(successors.values())
    regions=[]
    for i in range(len(lines)):
        if i in previous: continue
        chain=[lines[i]]
        while i in successors:
            i=successors[i];chain.append(lines[i])
        b=[min(x['box'][0] for x in chain),min(x['box'][1] for x in chain),
           max(x['box'][2] for x in chain),max(x['box'][3] for x in chain)]
        regions.append({'box':b,'confidence':None,'lines':[],'detectedLineCount':len(chain)})
    # A short orphan word can lie inside a multi-line group's rectangle.
    # Attach it instead of creating a second overlay within the same text region.
    for small in sorted(list(regions),key=lambda r:(r['box'][2]-r['box'][0])*(r['box'][3]-r['box'][1])):
        if small not in regions or small['detectedLineCount']!=1: continue
        a=small['box']; aa=(a[2]-a[0])*(a[3]-a[1])
        for large in regions:
            if large is small or large['detectedLineCount']<3: continue
            b=large['box']
            overlap=max(0,min(a[2],b[2])-max(a[0],b[0]))*max(0,min(a[3],b[3])-max(a[1],b[1]))
            if overlap/max(1,aa)>=.85:
                large['box']=[min(a[0],b[0]),min(a[1],b[1]),max(a[2],b[2]),max(a[3],b[3])]
                large['detectedLineCount']+=1
                regions.remove(small)
                break
    return regions
