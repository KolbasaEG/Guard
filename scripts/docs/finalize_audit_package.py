"""Create the traceability table, validation summary and final delivery archive."""
from pathlib import Path
import csv, hashlib, json, zipfile
from audit_content import SCENARIOS

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'docs/audit-package'
WORK=ROOT/'Guard/obj/audit-docs'
mapping={
    1:'P03 P05 P06',2:'P03 P05 P08',3:'P03 P04 P07',4:'P02 P06',5:'P04',
    6:'P05 P07',7:'P03 P05',8:'P04 P05',9:'P04 P05 P06',10:'P03 P04 P05',
    11:'P04 P05 P08',12:'P04 P05',13:'P05 P08',14:'P04 P08',15:'P04 P08',
    16:'P04 P08',17:'P03 P04',18:'P03 P04 P06',19:'P03 P04 P06',20:'P03 P06',
    21:'P01 P06',22:'P01 P06',23:'P06',24:'P01 P03 P06',25:'P02 P03',
    26:'P02 P06',27:'P05',28:'P02 P05 P09 P10',29:'P00',30:'P07',
    31:'P03 P07',32:'P03 P04 P09 P10',33:'P03 P05 P06',34:'P03 P04 P06',
    35:'P09 P10',36:'P03 P04 P06',37:'P01 P02',38:'P04',39:'P03 P04',
    40:'P02 P07',41:'P08',42:'P05 P08'}

qa=json.loads((WORK/'package-qa.json').read_text(encoding='utf-8'))
assert len(qa['documents'])==2 and qa['bpmnPages']==11
for d in qa['documents']:
    assert d['pages']==d['plannedPages'] and not d['positionErrors'] and not d['suspiciousPages']
manifest=json.loads((OUT/'source-manifest.json').read_text(encoding='utf-8'))
for item in manifest['files']:
    assert hashlib.sha256((ROOT/item['path']).read_bytes()).hexdigest()==item['sha256'],item['path']
with (OUT/'scenario-process-map.csv').open('w',encoding='utf-8-sig',newline='') as f:
    w=csv.writer(f,delimiter=';');w.writerow(['Сценарий','Наименование','Процессы BPMN'])
    for sc in SCENARIOS:w.writerow([sc[0],sc[1],mapping[int(sc[0][1:])]])
bpmn=json.loads((WORK/'bpmn-validation.json').read_text(encoding='utf-8'))
report={'edition':'1.0','date':'2026-10-20','branch':'codex/logging-reliability',
    'scope':'Documentation artifacts only; uncommitted implementation snapshot',
    'documents':[{k:d[k] for k in ('file','pages','tables','images','positionErrors','suspiciousPages')} for d in qa['documents']],
    'rootScenarios':42,'eventEnumValues':46,'bpmn':bpmn,
    'visualReview':{'technicalPages':22,'rootGuidePages':30,'bpmnPages':11,'unresolvedLayoutFindings':0},
    'sourceHashesVerified':len(manifest['files']),
    'checks':qa['checks']+['BPMN official XSD for master and 11 individual files','BPMN references and reachability','Source SHA-256 snapshot'],
    'notVerified':['Import into a specific BPMN editor','Real RuSIEM end-to-end acceptance','Production load capacity','Target network certificate revocation','Backup restore acceptance','Dedicated HTTP reproduction of L03-L05'],
    'productionCodeChangedForDocumentation':False}
(OUT/'validation-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
files=sorted(p for p in OUT.rglob('*') if p.is_file() and p.suffix!='.zip')
with zipfile.ZipFile(OUT/'Guard_Audit_Documentation.zip','w',zipfile.ZIP_DEFLATED) as z:
    for path in files:z.write(path,path.relative_to(OUT))
with zipfile.ZipFile(OUT/'Guard_Audit_Documentation.zip') as z:assert z.testzip() is None
print(json.dumps({'filesInArchive':len(files),'sourceHashesVerified':len(manifest['files']),
    'archiveBytes':(OUT/'Guard_Audit_Documentation.zip').stat().st_size},ensure_ascii=False))
