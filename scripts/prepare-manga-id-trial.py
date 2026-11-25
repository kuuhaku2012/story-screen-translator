"""Prepare one numbered contact sheet from automatic local boxes, without network access."""
from pathlib import Path
import json
from PIL import Image,ImageDraw,ImageFont

root=Path(__file__).resolve().parents[1]
data=root/'artifacts/manga-regression-172'
out=data/'id-trial'
out.mkdir(exist_ok=True)
image=Image.open(data/'input.png').convert('RGB')
assessment=json.loads((data/'local-regions-line-detector/assessment.json').read_text(encoding='utf-8'))
regions=[r for r in assessment['regions'] if not r['source'].strip().isdigit()]
sheet=Image.new('RGB',(960,((len(regions)+2)//3)*350),'#ddd')
draw=ImageDraw.Draw(sheet)
try: font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',22)
except OSError: font=ImageFont.load_default()
items=[]
for i,r in enumerate(regions):
    x1,y1,x2,y2=r['box']
    context=[max(0,int(x1)-10),max(0,int(y1)-10),min(image.width,int(x2)+11),min(image.height,int(y2)+11)]
    crop=image.crop(context)
    crop.thumbnail((300,300))
    factor=min(2,300/crop.width,300/crop.height)
    crop=crop.resize((round(crop.width*factor),round(crop.height*factor)),Image.Resampling.LANCZOS)
    left=(i%3)*320;top=(i//3)*350
    draw.rectangle((left+3,top+3,left+317,top+347),fill='white',outline='#777',width=1)
    draw.text((left+10,top+5),r['id'],font=font,fill='black')
    sheet.paste(crop,(left+(320-crop.width)//2,top+38))
    items.append({'id':r['id'],'box':[x1/image.width*1000,y1/image.height*1000,
                                   (x2-x1)/image.width*1000,(y2-y1)/image.height*1000],
                  'localOcr':r['source'],'contextPixels':context})
sheet.save(out/'id-input.png')
(out/'manifest.json').write_text(json.dumps({'sourceImage':str(data/'input.png'),'regions':items},ensure_ascii=False,indent=2),encoding='utf-8')
print(f'Prepared {len(items)} automatic region IDs; no image sent. Sheet: {sheet.size}')
