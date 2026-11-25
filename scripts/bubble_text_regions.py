"""Fuse independent bubble proposals with detected text lines, no reference labels."""
from manga_line_regions import group_lines

def fuse_regions(bubbles, lines):
    # Group before bubble assignment: a contour boundary must not split continuous text.
    groups=group_lines(lines)
    # Italic/tall detected lines can overlap an existing cluster without satisfying
    # its normal inter-line height rule. Absorb spatially overlapping fragments only.
    changed=True
    while changed:
        changed=False
        for i in range(len(groups)):
            if changed:break
            for j in range(i+1,len(groups)):
                a=groups[i]['box'];b=groups[j]['box']
                ow=max(0,min(a[2],b[2])-max(a[0],b[0]));oh=max(0,min(a[3],b[3])-max(a[1],b[1]))
                aa=(a[2]-a[0])*(a[3]-a[1]);ba=(b[2]-b[0])*(b[3]-b[1]);fraction=ow*oh/max(1,min(aa,ba))
                dy=abs((a[1]+a[3]-b[1]-b[3])/2)
                if fraction>=.4 and ow>=.65*min(a[2]-a[0],b[2]-b[0]) and (dy>=.25*min(a[3]-a[1],b[3]-b[1]) or fraction>=.9):
                    groups[i]['box']=[min(a[0],b[0]),min(a[1],b[1]),max(a[2],b[2]),max(a[3],b[3])]
                    groups[i]['detectedLineCount']+=groups[j]['detectedLineCount'];groups.pop(j);changed=True;break
    assigned=[];retained=set()
    for group in groups:
        a=group['box']
        area=(a[2]-a[0])*(a[3]-a[1])
        if area<=0:continue
        matches=[]
        for i,p in enumerate(bubbles):
            b=p['box'];hit=max(0,min(a[2],b[2])-max(a[0],b[0]))*max(0,min(a[3],b[3])-max(a[1],b[1]))
            if hit/area>=.8:matches.append(((b[2]-b[0])*(b[3]-b[1]),i))
        key=min(matches)[1] if matches else -1
        assigned.append((group,key))
        if key>=0:retained.add(key)
    regions=[]
    for g,key in assigned:
        a=g['box'];region=list(a);kind='floating-text'
        if key>=0:
            count=sum(k==key for _,k in assigned)
            kind='cluster-in-connected-bubble' if count>1 else 'bubble'
            if count==1:
                b=bubbles[key]['box']
                # Text support does not prove a large bright component is a balloon.
                # Bound expansion by estimated line height, always preserving the full text box.
                margin=max(6,1.5*(a[3]-a[1])/max(1,g['detectedLineCount']))
                centered=abs((a[0]+a[2]-b[0]-b[2])/2)<=.22*(b[2]-b[0]) and abs((a[1]+a[3]-b[1]-b[3])/2)<=.22*(b[3]-b[1])
                if centered:
                    region=[min(a[0],max(b[0],a[0]-margin)),min(a[1],max(b[1],a[1]-margin)),
                            max(a[2],min(b[2],a[2]+margin)),max(a[3],min(b[3],a[3]+margin))]
                else:kind='text-in-bright-region'
        regions.append({'textBounds':a,'regionBounds':region,'bubble':key if key>=0 else None,'kind':kind,'lineCount':g['detectedLineCount']})
    return {'regions':sorted(regions,key=lambda g:(g['textBounds'][1],g['textBounds'][0])),
            'retainedBubbleCount':len(retained),'removedBubbleCount':len(bubbles)-len(retained)}
