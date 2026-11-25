"""Download pinned official model; no user text/images sent."""
from pathlib import Path
import urllib.request,json,hashlib
root=Path(__file__).resolve().parents[1]
out=root/'models/ocr-evaluation/ppocrv6-medium';out.mkdir(exist_ok=True)
revision='50c7eacafc52fa7bcf4194e8cd08e46f8558504b'
expected='9c09abf0957f7968c7586464b7397b84ad2387a0497a351af40e9acc71b673ba'
base=f'https://huggingface.co/PaddlePaddle/PP-OCRv6_medium_rec_onnx/resolve/{revision}/'
records=[]
for name in ['inference.onnx','inference.yml','inference.json','README.md']:
    path=out/name
    if not path.exists():
        partial=out/(name+'.partial')
        with urllib.request.urlopen(base+name,timeout=90) as source,partial.open('wb') as target:
            while chunk:=source.read(1024*1024):target.write(chunk)
        digest=hashlib.sha256(partial.read_bytes()).hexdigest()
        if name=='inference.onnx' and digest!=expected:raise ValueError('ONNX hash mismatch; partial not promoted')
        partial.replace(path)
    digest=hashlib.sha256(path.read_bytes()).hexdigest()
    if name=='inference.onnx' and digest!=expected:raise ValueError('Existing ONNX hash mismatch')
    records.append({'file':name,'bytes':path.stat().st_size,'sha256':digest})
(out/'provenance.json').write_text(json.dumps({'repository':'PaddlePaddle/PP-OCRv6_medium_rec_onnx','revision':revision,'license':'apache-2.0 per official model card','files':records},indent=2),encoding='utf8')
print('PASS: pinned official files downloaded; ONNX SHA256 matches published LFS metadata.')
