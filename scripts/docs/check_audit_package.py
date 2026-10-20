"""Read-only package QA plus private raster previews and a machine-readable report."""
from pathlib import Path
import json,re,zipfile,hashlib
import pypdfium2 as pdfium
import pdfplumber
from lxml import etree as E
from PIL import Image,ImageOps,ImageDraw
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'docs/audit-package';WORK=ROOT/'Guard/obj/audit-docs'
NS={'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}

def inspect_doc(name,folder,expected):
    pdf=WORK/folder/(name+'.pdf')
    with pdfplumber.open(pdf) as doc:
        pages=[p.extract_text() or '' for p in doc.pages]
        actual=[]
        for title,bookmark,expected_page in expected['toc']:
            # Static TOC is on page 2, so inspect body pages only.
            matches=[i+1 for i,text in enumerate(pages) if i>=2 and text.startswith(title)]
            actual.append({'heading':title,'planned':expected_page,'actual':matches})
        report={'file':name+'.docx','pages':len(pages),'plannedPages':expected['plannedPages'],
            'sectionPositions':actual,'characters':[len(x) for x in pages]}
        report['positionErrors']=[x for x in actual if x['actual'] != [x['planned']]]
        report['suspiciousPages']=[i+1 for i,text in enumerate(pages) if len(text)<100 and i<len(pages)-1]
        assert len(pages)==expected['plannedPages'],(name,'unexpected page count')
        assert not report['positionErrors'],(name,report['positionErrors'])
        assert not report['suspiciousPages'],(name,'empty page')
        if 'Root' in name:
            for n in range(1,43):assert any(re.search(r'\bR'+str(n).zfill(2)+r'\b',text) for text in pages),n
        for p in doc.pages:
            for word in p.extract_words():
                assert word['x0']>=-1 and word['x1']<=p.width+1,(name,p.page_number,'horizontal text overflow')
                assert word['top']>=-1 and word['bottom']<=p.height+1,(name,p.page_number,'vertical text overflow')
    with zipfile.ZipFile(OUT/(name+'.docx')) as z:
        xml=E.fromstring(z.read('word/document.xml'))
        anchors=set(xml.xpath('//w:bookmarkStart/@w:name',namespaces=NS))
        assert set(xml.xpath('//w:hyperlink/@w:anchor',namespaces=NS))<=anchors
        text=' '.join(xml.xpath('//w:t/text()',namespaces=NS))
        assert 'BrowserRoot123!' not in text and 'root@browser.test' not in text
        assert 'TODO' not in text
        report['tables']=len(xml.xpath('//w:tbl',namespaces=NS));report['images']=len(xml.xpath('//w:drawing',namespaces=NS))
    return report

def thumbnails(paths,target):
    width=330;height=490;sheet=Image.new('RGB',(width*4,height*((len(paths)+3)//4)),'#e4e9eb');draw=ImageDraw.Draw(sheet)
    for i,path in enumerate(paths):
        im=Image.open(path).convert('RGB');im.thumbnail((width-12,height-28));x=(i%4)*width+6;y=(i//4)*height+22
        sheet.paste(im,(x,y));draw.text((x,y-18),path.parent.name+'/'+path.name,fill='black')
    sheet.save(target)

def main():
    structure=json.loads((WORK/'doc-structure.json').read_text(encoding='utf-8'))
    reports=[]
    for expected,folder in zip(structure,['render-tech','render-root']):
        name=Path(expected['file']).stem
        if (WORK/folder/(name+'.pdf')).exists():reports.append(inspect_doc(name,folder,expected))
    atlas=pdfium.PdfDocument(OUT/'BPMN/Guard_Audit_BPMN_Atlas.pdf');dest=WORK/'render-bpmn';dest.mkdir(exist_ok=True)
    for i in range(len(atlas)):
        page=atlas[i];page.render(scale=1.25).to_pil().save(dest/f'page-{i+1}.png')
    for folder in ['render-tech','render-root','render-bpmn']:
        paths=sorted((WORK/folder).glob('page-*.png'),key=lambda p:int(p.stem.split('-')[-1]))
        if paths:thumbnails(paths,WORK/(folder+'-contact.png'))
    report={'documents':reports,'bpmnPages':len(atlas),'checks':['OOXML bookmarks','all 42 scenarios','no sample credentials in document text','PDF text bounds','section pagination']}
    (WORK/'package-qa.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(report,ensure_ascii=False))

if __name__=='__main__':main()
