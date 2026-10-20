"""Build descriptive BPMN 2.0 XML, DI, SVG and a printable atlas from one model.
Run with the Python runtime returned by load_workspace_dependencies.
"""
from __future__ import annotations
import heapq, json, math, re, textwrap
from pathlib import Path
from lxml import etree as E
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib.pagesizes import A3, A2, landscape
from reportlab.lib.colors import HexColor

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'docs/audit-package/BPMN'
WORK = ROOT / 'Guard/obj/audit-docs'
NS = {'bpmn':'http://www.omg.org/spec/BPMN/20100524/MODEL',
      'bpmndi':'http://www.omg.org/spec/BPMN/20100524/DI',
      'dc':'http://www.omg.org/spec/DD/20100524/DC',
      'di':'http://www.omg.org/spec/DD/20100524/DI',
      'xsi':'http://www.w3.org/2001/XMLSchema-instance'}
SVG='http://www.w3.org/2000/svg'
PROCESSES=[]

def process(code,title,lanes,doc,siem=False):
    p={'id':code,'title':title,'lanes':lanes,'doc':doc,'nodes':[],'edges':[], 'siem':siem}
    PROCESSES.append(p)
    return p
def n(p,id,kind,label,col,lane=0,row=0,call=None,attach=None,timer=None,doc=''):
    p['nodes'].append(dict(id=p['id']+'_'+id,short=id,kind=kind,label=label,
        col=col,lane=lane,row=row,call=call,attach=attach,timer=timer,doc=doc))
def f(p,a,b,label='',default=False,via=None):
    p['edges'].append(dict(id=p['id']+'_F'+str(len(p['edges'])+1),source=p['id']+'_'+a,
        target=p['id']+'_'+b,label=label,default=default,via=via))

p=process('P00','Обращение к системе и выбор процесса',['Пользователь или Root','Guard'],
 'Карта процессов. Доставка P04 и очистка P07 работают независимо по таймеру; P03 вызывается запросами и циклом состояния каждые 5 секунд. Модель описательная, не предназначена для запуска workflow engine.')
for args in [('start','start','Обращение',0,0),('classify','service','Определить маршрут и тип операции',1,1),
 ('kind','xor','Какое действие',2,1),('login','call','Вход в систему P01',3,0,0,'P01'),
 ('work','call','Рабочая операция P02',3,1,0,'P02'),('settings','call','Настройки Root P05',3,1,1,'P05'),
 ('csrf','service','Проверить CSRF выхода',4,0),('out','service','Удалить cookie и попытаться записать Logout',5,0),
 ('end','end','Результат и дальнейшее действие',6,1)]: n(p,*args)
f(p,'start','classify');f(p,'classify','kind')
for target,label in [('login','Вход'),('work','Работа'),('settings','Настройки'),('csrf','Выход')]: f(p,'kind',target,label)
f(p,'csrf','out');f(p,'out','end');f(p,'login','end');f(p,'work','end');f(p,'settings','end')

p=process('P01','Обычный вход и отказ в доступе',['Клиент и HTTP','Identity и доступ','Обязательный аудит'],
 'Одна попытка входа имеет итоговое событие. Смена счётчика и наступление lockout могут иметь дополнительные связанные события. Успешная cookie выдаётся только после LoginSuccess. Включённая 2FA в локальном сценарии не поддерживается.')
for args in [('start','start','POST входа',0,0),('rate','service','Лимит 5 запросов на IP в минуту',1,0),
 ('rg','xor','Лимит соблюдён',2,0),('form','service','Проверить форму и обязательные поля',3,0),
 ('probe','call','Проверить аудит P03',4,0,0,'P03'),('ag','xor','Обычная работа разрешена',5,0),
 ('recovery','call','Ограниченный вход Root P06',6,0,0,'P06'),
 ('identity','service','Найти аккаунт и проверить пароль политику IP и 2FA',4,1),('ig','xor','Вход разрешён',5,1),
 ('reject','service','Попытаться сохранить итог отказа и причину',2,1,1),('denied','end','Обычный доступ не предоставлен',3,1,1),
 ('record','service','Сохранить LoginSuccess до cookie',5,2),('sg','xor','Запись подтверждена',6,2),
 ('cookie','service','Выдать обычную cookie и обновить активность',7,1),('ok','end','Обычная сессия',8,1),
 ('unknown','end','Исход сохранения неизвестен проверить состояние',7,2,1)]: n(p,*args)
f(p,'start','rate');f(p,'rate','rg');f(p,'rg','form','Да');f(p,'rg','reject','Нет HTTP 429',True)
f(p,'form','probe','Форма корректна');f(p,'form','reject','InvalidInput');f(p,'probe','ag')
f(p,'ag','identity','Да');f(p,'ag','recovery','Нет',True);f(p,'recovery','denied','Обычная cookie не выдаётся')
f(p,'identity','ig');f(p,'ig','record','Да');f(p,'ig','reject','Нет',True);f(p,'reject','denied')
f(p,'record','sg');f(p,'sg','cookie','Да');f(p,'sg','denied','Доказанный отказ');f(p,'sg','unknown','Подтверждение потеряно',True);f(p,'cookie','ok')
n(p,'formok','xor','Форма корректна',3,0,1)
p['edges']=[ed for ed in p['edges'] if not(ed['source']=='P01_form')]
f(p,'form','formok');f(p,'formok','probe','Да');f(p,'formok','reject','Нет InvalidInput',True)

p=process('P02','Обычная операция и атомарность аудита',['Сервис доступа','Чтение данных','Транзакция изменения'],
 'Проверки остаются на сервере, включая старый Blazor circuit. Состояние P03 проверяет текущую очередь; перед обязательной вставкой дополнительно резервируется место под новые события. Обычные чтения самостоятельных событий не создают.')
for args in [('start','start','Запрос операции',0,0),('session','service','Проверить stamp роли область и отсутствие recovery only',1,0),
 ('probe','call','Проверить аудит P03',2,0,0,'P03'),('gate','xor','Доступ разрешён',3,0),
 ('kind','xor','Тип операции',4,0),('deny','end','Операция запрещена',5,0,1),
 ('query','service','Прочитать DTO в серверной области',4,1),('recheck','service','Перепроверить доступ перед возвратом',5,1),
 ('return','end','Вернуть данные без события просмотра',6,1),
 ('begin','service','Начать транзакцию и сохранить изменение',3,2),('insert','service','Под QueueLock проверить лимиты и вставить обязательный аудит',4,2),
 ('commit','service','Выполнить общий commit',5,2),('cg','xor','Результат commit',6,2),
 ('done','end','Изменение и аудит сохранены',7,2),('rollback','service','Откатить известную незавершённую транзакцию',4,2,1),
 ('unknown','end','Проверить фактическое состояние до повтора',7,2,1),('export','call','Экспорт P09',6,0,0,'P09')]: n(p,*args)
for a,b in [('start','session'),('session','probe'),('probe','gate'),('gate','kind'),('kind','query'),('query','recheck'),('recheck','return'),('kind','begin'),('begin','insert'),('insert','commit'),('commit','cg'),('rollback','deny')]: f(p,a,b)
f(p,'gate','deny','Нет',True);f(p,'kind','export','Экспорт');f(p,'recheck','deny','Доступ изменился');f(p,'insert','rollback','Отказ до commit')
f(p,'cg','done','Подтверждён');f(p,'cg','rollback','Откат подтверждён');f(p,'cg','unknown','Подтверждение потеряно',True)
f(p,'export','done','Задание принято с аудитом')
n(p,'readok','xor','Повторная проверка успешна',6,1)
n(p,'writeError','boundaryError','Ошибка до commit',4,2,1,attach='insert')
next(v for v in p['nodes'] if v['short']=='return')['col']=7
next(v for v in p['nodes'] if v['short']=='rollback')['col']=3
p['edges']=[ed for ed in p['edges'] if not(ed['source']=='P02_recheck') and not(ed['source']=='P02_insert' and ed['target']=='P02_rollback')]
f(p,'recheck','readok');f(p,'readok','return','Да');f(p,'readok','deny','Нет',True);f(p,'writeError','rollback')

p=process('P03','Определение доступности аудита',['Проверка локального хранения','Настройки и очередь'],
 'Один вызов проверки. Фоновый вызов повторяется каждые 5 секунд; HTTP и серверные операции вызывают проверку независимо. Проба вставляется и удаляется в одной подтверждённой транзакции, не создавая историю просмотров.')
for args in [('start','start','Запрос проверки',0,0),('probe','service','UPDATE настроек INSERT DELETE пробы и commit',1,0),
 ('store','xor','Commit пробы подтверждён',2,0),('storage','end','StorageUnavailable',3,0,1),
 ('config','service','Прочитать подтверждение режима и текущую очередь',3,1),('configured','xor','Режим подтверждён',4,1),
 ('not','end','NotConfigured',4,0),('valid','xor','Настройки допустимы',5,1),('invalid','end','InvalidConfiguration',5,0),
 ('mode','xor','Локальный режим',6,1),('limits','service','Проверить возраст байты и количество pending',6,1,1),
 ('limit','xor','Пределы соблюдены',7,1,1),('ready','end','Ready обычная работа разрешена',7,0),
 ('blocked','end','QueueAgeExceeded или QueueVolumeExceeded',8,1,1)]: n(p,*args)
f(p,'start','probe');f(p,'probe','store');f(p,'store','config','Да');f(p,'store','storage','Нет или неизвестен',True)
f(p,'config','configured');f(p,'configured','valid','Да');f(p,'configured','not','Нет',True)
f(p,'valid','mode','Да');f(p,'valid','invalid','Нет',True);f(p,'mode','ready','Да');f(p,'mode','limits','Нет SIEM',True)
f(p,'limits','limit');f(p,'limit','ready','Да');f(p,'limit','blocked','Нет',True)
next(v for v in p['nodes'] if v['short']=='probe')['label']='Прочитать настройки и очередь выполнить INSERT DELETE пробы и commit'
next(v for v in p['nodes'] if v['short']=='config')['label']='Оценить прочитанные настройки и очередь'

p=process('P04','Доставка обязательной очереди в RuSIEM',['Worker и PostgreSQL','TLS транспорт','Фиксация попытки'],
 'Цикл доставки каждые 2 секунды, до 25 событий. Аренда 2 минуты. Pending сохраняются при любых сетевых отказах. Успешная запись по TLS не является подтверждением сохранения в SIEM. При падении процесса возможен повтор того же EventId.',True)
for args in [('start','timerStart','Цикл каждые 2 секунды',0,0),('mode','service','Прочитать настройки под QueueLock',1,0),
 ('enabled','xor','SIEM включён и настроен',2,0),('paused','end','В Local очередь сохранена и приостановлена',2,0,1),
 ('claim','service','Выбрать pending SKIP LOCKED и выдать lease на 2 минуты',3,0),('exists','xor','Событие найдено',4,0),
 ('attempt','service','Сохранить начало попытки адресат и Attempts',5,0),('send','send','Проверить TLS и записать Syslog CEF',5,1),
 ('result','xor','Запись по TLS завершена',6,1),('success','service','Записать SentAtUtc и успешную попытку',7,0),
 ('retry','service','Записать ошибку и следующую попытку 10 с до 1 ч',7,2),
 ('batch','xor','Менее 25 событий и нет отмены',8,0),('done','end','Ожидать следующий цикл',8,0,1),
 ('crash','end','При обрыве lease истечёт и исход попытки останется неизвестным',6,2,1)]: n(p,*args)
p['nodes'][0]['timer']='PT2S'
f(p,'start','mode');f(p,'mode','enabled');f(p,'enabled','claim','Да');f(p,'enabled','paused','Нет',True)
f(p,'claim','exists');f(p,'exists','attempt','Да');f(p,'exists','done','Нет',True);f(p,'attempt','send');f(p,'send','result')
f(p,'result','crash','Отмена процесса');f(p,'result','success','Да');f(p,'result','retry','Нет',True)
f(p,'success','batch');f(p,'retry','batch');f(p,'batch','claim','Да');f(p,'batch','done','Нет',True)

p=process('P05','Изменение настроек и переключение режимов',['Root','Сервер и блокировки','Транзакция аудита'],
 'Настройки применяются только после проверки актуального Root, версии и обязательного события. Изменение host port или CA при pending требует отдельного подтверждения. Старые Local события не ретроотправляются; возраст прежней обязательной очереди сохраняется.')
for args in [('start','start','Сохранить настройки',0,0),('validate','service','Проверить поля и актуального Root',1,1),
 ('lock','service','Начать транзакцию взять security lock и QueueLock',2,1),('version','xor','Root и версия актуальны',3,1),
 ('denied','end','Отказ перечитать настройки или войти заново',3,0),('target','xor','Адресат изменён и есть pending',4,1),
 ('confirm','user','Отдельно подтвердить перенос обязательной очереди',4,0),('consent','xor','Подтверждение есть',5,0),
 ('lease','xor','Есть активная отправка при смене режима или адресата',5,1),
 ('wait','end','Повторить после отправки или истечения аренды',5,1,1),
 ('save','service','Сохранить версию режим и при необходимости назначение pending',6,2),
 ('audit','service','Вставить AuditConfigurationChanged в ту же транзакцию',7,2),
 ('commit','xor','Общий commit подтверждён',8,2),('ok','end','Настройки действуют',8,1),
 ('unknown','end','Результат неизвестен перечитать фактическую версию',8,2,1)]: n(p,*args)
for a,b in [('start','validate'),('validate','lock'),('lock','version'),('version','target'),('target','confirm'),('confirm','consent'),('consent','lease'),('target','lease'),('lease','save'),('save','audit'),('audit','commit')]: f(p,a,b)
f(p,'version','denied','Нет',True);f(p,'consent','denied','Нет',True);f(p,'lease','wait','Да',True)
f(p,'commit','ok','Да');f(p,'commit','denied','Известный отказ откат');f(p,'commit','unknown','Подтверждение потеряно',True)
next(v for v in p['nodes'] if v['short']=='audit')['label']='Вставить событие настроек и выполнить общий commit'

p=process('P06','Ограниченный вход и восстановление Root',['Root','Identity и ограниченная сессия','Восстановление'],
 'Ограниченный вход выдаётся только при запрещённой обычной работе и доступной Identity. Проверяется фактическая роль Root, hash пароля, блокировки и отсутствие 2FA. Неверная recovery попытка не увеличивает Identity failure counter и не создаёт отдельный LoginFailed внутри RecoveryAsync.')
for args in [('start','start','Аудит запрещает обычный вход',0,0),('rate','service','Применить отдельный лимит 5 на IP в минуту',1,1),
 ('identity','xor','Identity доступна',2,1),('external','end','Выход и обслуживание сервера',2,2),
 ('verify','service','Проверить пароль Root блокировки и отсутствие 2FA',3,1),('valid','xor','Условия соблюдены',4,1),
 ('refuse','end','Общий отказ без cookie',4,0),('cookie','service','Выдать recovery only cookie на 15 минут без продления',5,1),
 ('event','service','Попытаться записать RecoverySignIn',6,1),('diagnose','user','Открыть состояние и определить причину',6,0),
 ('repair','user','Восстановить хранилище или соединение с сопровождением',5,2),
 ('settings','call','При исправной локальной записи изменить настройки P05',6,2,0,'P05'),
 ('health','call','Повторить проверку P03',7,2,0,'P03'),('working','xor','Аудит исправен',8,2),
 ('login','user','Выйти и выполнить обычный вход',8,1),('end','end','Recovery сессия не повышается автоматически',8,0),
 ('ttl','start','Новый HTTP с истёкшей cookie',7,0),
 ('expired','end','Повторить ограниченный вход',7,0,1)]: n(p,*args)
for a,b in [('start','rate'),('rate','identity'),('identity','verify'),('verify','valid'),('valid','cookie'),('cookie','event'),('event','diagnose'),('diagnose','repair'),('repair','settings'),('settings','health'),('health','working'),('working','login'),('login','end'),('ttl','expired')]:f(p,a,b)
f(p,'identity','external','Нет',True);f(p,'valid','refuse','Нет',True);f(p,'working','diagnose','Нет',True)
n(p,'rateok','xor','Лимит допускает',1,1,1)
n(p,'needsettings','xor','Нужно менять настройки',5,2,1)
p['edges']=[ed for ed in p['edges'] if not(ed['source']=='P06_rate') and not(ed['source']=='P06_repair')]
f(p,'rate','rateok');f(p,'rateok','identity','Да');f(p,'rateok','refuse','Нет',True)
f(p,'repair','needsettings');f(p,'needsettings','settings','Да');f(p,'needsettings','health','Нет',True)

p=process('P07','Хранение и независимая очистка',['Настройки','Хранилища и файлы','Диагностика'],
 'Цикл каждые 5 минут. Срок от даты события в календарных годах. Неотправленные обязательные события сохраняются в обоих режимах. Ошибка одного этапа не отменяет следующие. Последний корректный срок кэшируется в процессе.')
for args in [('start','timerStart','Цикл каждые 5 минут',0,0),('read','service','Получить срок или последний корректный в процессе',1,0),
 ('years','xor','Есть допустимый срок',2,0),('no','end','Показать ошибку настроек без удаления',2,2),
 ('cutoff','service','Вычислить UTC cutoff AddYears',3,0),('audit','service','Удалить старые Local и доставленные до 10 пакетов по 1000',3,1),
 ('ae','boundaryError','Ошибка Audit',3,1,1,None,'audit'),('aerr','service','Запомнить ошибку Audit',4,2),
 ('am','xor','Продолжить',4,1),('technical','service','Удалить старые технические строки до 10 пакетов по 10000',5,1),
 ('te','boundaryError','Ошибка Technical',5,1,1,None,'technical'),('terr','service','Запомнить ошибку Technical',6,2),
 ('tm','xor','Продолжить',6,1),('files','service','Удалить только собственные старые лог файлы',7,1),
 ('fe','boundaryError','Ошибка Files',7,1,1,None,'files'),('ferr','service','Запомнить ошибку Files',8,2),
 ('fm','xor','Продолжить',8,1),('health','service','Обновить LastCleanupUtc и ошибки этапов',8,0),
 ('done','end','Очистка завершена или частична',9,0)]:n(p,*args)
for a,b in [('start','read'),('read','years'),('years','cutoff'),('cutoff','audit'),('audit','am'),('ae','aerr'),('aerr','am'),('am','technical'),('technical','tm'),('te','terr'),('terr','tm'),('tm','files'),('files','fm'),('fe','ferr'),('ferr','fm'),('fm','health'),('health','done')]:f(p,a,b)
f(p,'years','no','Нет',True);p['nodes'][0]['timer']='PT5M'

p=process('P08','Проверка TLS соединения и тестового события',['Root','Guard и журнал','TLS транспорт'],
 'Проверка использует поля формы без сохранения настроек. Начало и результат записываются разными EventId. В RuSIEM нужно искать EventId начала, указанный в сообщении интерфейса. Подтверждение приёма выполняет Root отдельно.',True)
for args in [('start','start','Проверить соединение',0,0),('validate','service','Проверить актуального Root адрес порт и публичный CA',1,1),
 ('intent','service','Сохранить started и EventId теста локально',2,1),('local','xor','Локальная запись доступна',3,1),
 ('blocked','end','Тест запрещён до восстановления записи',3,0),('send','send','Выполнить TLS и Syslog CEF в адрес из формы',4,2),
 ('sent','xor','Запись по TLS завершена',5,2),('fail','service','Сохранить failure с новым EventId',5,1,1),
 ('success','service','Сохранить success с новым EventId',6,1),('result','xor','Подтверждение результата получено',7,1),
 ('unknown','end','Проверить фактическое состояние до повтора',7,2,1),
 ('find','user','Найти исходный EventId в RuSIEM и проверить поля',7,0),('found','xor','Событие найдено и разобрано',8,0),
 ('ok','end','Сквозная проверка подтверждена Root',9,0),('unverified','end','Проверка не завершена передать администратору SIEM',9,1)]:n(p,*args)
for a,b in [('start','validate'),('validate','intent'),('intent','local'),('local','send'),('send','sent'),('sent','success'),('sent','fail'),('fail','unverified'),('success','result'),('result','find'),('find','found')]:f(p,a,b)
f(p,'local','blocked','Нет',True);f(p,'result','unknown','Commit неизвестен',True);f(p,'found','ok','Да');f(p,'found','unverified','Нет',True)

p=process('P09','Создание и формирование экспорта',['Пользователь и сервис','Очередь и worker','Файл и обязательный аудит'],
 'Запрос и изменения состояний экспорта сопровождаются обязательным аудитом. Worker при неисправном аудите не начинает новую обработку. На контрольных точках повторяются проверки. После restart Processing возвращается в очередь с событием восстановления.')
for args in [('start','start','Запрос экспорта',0,0),('access','service','Проверить permissions область запрос и аудит',1,0),
 ('request','service','Сохранить задание и ExportRequested одной транзакцией',2,0),('queued','service','Ожидать исправного аудита и свободного worker',3,1),
 ('claim','service','Захватить задание и сохранить ProcessingStarted',4,1),
 ('make','service','Повторять проверки и сформировать файл',5,2),('mg','xor','Формирование успешно',6,2),
 ('ready','service','Сохранить артефакт Ready и ExportArchived одной транзакцией',7,2),
 ('download','call','Выдача и завершение P10',8,0,0,'P10'),
 ('failed','service','Сохранить Failed или Cancelled и итоговый аудит',7,1),('end','end','Результат задания доступен при исправном аудите',8,1),
 ('restart','start','Worker после перезапуска',3,2,1),('recover','service','Processing вернуть в Queued с событием восстановления',4,2,1)]:n(p,*args)
for a,b in [('start','access'),('access','request'),('request','queued'),('queued','claim'),('claim','make'),('make','mg'),('mg','ready'),('ready','download'),('mg','failed'),('failed','end'),('download','end'),('restart','recover'),('recover','queued')]:f(p,a,b)
n(p,'downloadRequest','catchMessage','Запрос пользователя на получение файла',8,2)
p['edges']=[ed for ed in p['edges'] if not(ed['source']=='P09_ready' and ed['target']=='P09_download')]
f(p,'ready','downloadRequest');f(p,'downloadRequest','download')

p=process('P10','Выдача экспорта и неизвестный результат',['Пользователь и доступ','Передача файла','Архив и восстановление'],
 'Передача одноразовая. Проверки доступа и аудита продолжаются при чтении. SentAt аудита и результат передачи файла различаются. При перезапуске незавершённая выдача не объявляется успешной: попытка unknown, задание Failed, требуется новый экспорт.')
for args in [('start','start','Получить готовый файл',0,0),('guard','service','Проверить права stamp срок артефакт и аудит',1,0),
 ('begin','service','Захватить одноразовую выдачу и записать DeliveryStarted',2,0),
 ('stream','service','Передавать файл с повторными проверками',3,1),('result','xor','Итог передачи известен',4,1),
 ('complete','service','Записать DeliveryCompleted и фактический результат',5,1),
 ('unknown','service','После restart отметить попытку unknown и задание Failed',4,2),
 ('new','user','При неизвестном исходе создать новый экспорт',5,2),
 ('expire','service','По сроку удалить артефакт и записать ExportExpired',6,2),
 ('end','end','Файл завершён или требуется новое задание',7,1),
 ('crash','start','Worker после перезапуска',3,1,1)]:n(p,*args)
for a,b in [('start','guard'),('guard','begin'),('begin','stream'),('stream','result'),('result','complete'),('result','unknown'),('crash','unknown'),('unknown','new'),('new','end'),('complete','end'),('complete','expire'),('expire','end')]:f(p,a,b)
n(p,'expiryCheck','timerStart','Проверка сроков каждые 30 секунд',6,2,1,timer='PT30S')
p['edges']=[ed for ed in p['edges'] if not(ed['source']=='P10_complete' and ed['target']=='P10_expire')]
f(p,'expiryCheck','expire')

def tag(ns,local_name,**attrs): return E.Element('{'+NS[ns]+'}'+local_name,**{k:str(v) for k,v in attrs.items()})
def child(parent,ns,local_name,text=None,**attrs):
    e=tag(ns,local_name,**attrs);parent.append(e)
    if text is not None:e.text=str(text)
    return e
def box(node):
    if node['kind'] in ('start','end','timerStart','boundaryTimer','boundaryError','catchMessage'):w=h=40
    elif node['kind']=='xor':w=h=60
    else:w,h=160,80
    cx=170+node['col']*220;cy=170+node['lane']*230+node['row']*110
    return (cx-w/2,cy-h/2,w,h)
def wrap(label,width=20):
    words=label.split();lines=[];line=''
    for w in words:
        if len(line)+len(w)+1>width and line:lines.append(line);line=w
        else:line=(line+' '+w).strip()
    if line:lines.append(line)
    return lines
def route(a,b,obstacles):
    ax,ay,aw,ah=a;bx,by,bw,bh=b
    ac=(ax+aw/2,ay+ah/2);bc=(bx+bw/2,by+bh/2)
    s=(ax+aw,ac[1]);t=(bx,bc[1]);ss=(s[0]+20,s[1]);tt=(t[0]-20,t[1])
    start=(round(ss[0]/10),round(ss[1]/10));end=(round(tt[0]/10),round(tt[1]/10))
    xmax=int(max(r[0]+r[2] for r in obstacles)/10)+20;ymax=int(max(r[1]+r[3] for r in obstacles)/10)+22
    blocked=set()
    for x,y,w,h in obstacles:
        for xx in range(math.floor((x-9)/10),math.ceil((x+w+9)/10)+1):
            for yy in range(math.floor((y-9)/10),math.ceil((y+h+9)/10)+1):blocked.add((xx,yy))
    # Boundary events touch an activity: open a short port corridor out of both shapes.
    for origin in (start,end):
        for dx in range(-3,4):
            for dy in range(-3,4):blocked.discard((origin[0]+dx,origin[1]+dy))
    heap=[(0,0,start,-1)];seen={};previous={};finish=None
    while heap:
        _,cost,pos,direction=heapq.heappop(heap);key=(pos,direction)
        if key in seen and seen[key]<cost:continue
        if pos==end:finish=key;break
        for d,(dx,dy) in enumerate(((1,0),(0,1),(-1,0),(0,-1))):
            v=(pos[0]+dx,pos[1]+dy)
            if v in blocked or not(6<=v[0]<=xmax and 7<=v[1]<=ymax):continue
            nc=cost+1+(0 if d==direction or direction==-1 else 3)
            nk=(v,d)
            if nc<seen.get(nk,1e9):
                seen[nk]=nc;previous[nk]=key
                heuristic=abs(v[0]-end[0])+abs(v[1]-end[1])
                heapq.heappush(heap,(nc+heuristic,nc,v,d))
    if finish is None:raise RuntimeError(f'No route {a} {b}')
    pts=[];k=finish
    while k in previous:pts.append((k[0][0]*10,k[0][1]*10));k=previous[k]
    pts.append((start[0]*10,start[1]*10));pts.reverse();pts=[s,*pts,t]
    compact=[]
    for pt in pts:
        if compact and pt==compact[-1]:continue
        if len(compact)>=2 and ((compact[-2][0]==compact[-1][0]==pt[0]) or (compact[-2][1]==compact[-1][1]==pt[1])):compact[-1]=pt
        else:compact.append(pt)
    return compact

def prepare(p):
    nodes={v['id']:v for v in p['nodes']}
    explicit={
      ('P02_kind','P02_query'):'Чтение',('P02_kind','P02_begin'):'Изменение',
      ('P05_target','P05_confirm'):'Да',('P05_target','P05_lease'):'Нет',
      ('P05_lease','P05_save'):'Нет активной отправки',
      ('P08_sent','P08_success'):'Да',('P08_sent','P08_fail'):'Нет',
      ('P09_mg','P09_ready'):'Да',('P09_mg','P09_failed'):'Нет',
      ('P10_result','P10_complete'):'Да',('P10_result','P10_unknown'):'Нет'}
    for ed in p['edges']:
        if (ed['source'],ed['target']) in explicit:ed['label']=explicit[(ed['source'],ed['target'])]
        if nodes[ed['source']]['kind']=='xor' and not ed['label'] and sum(x['source']==ed['source'] for x in p['edges'])>1:ed['label']='Да'
    for v in p['nodes']:v['box']=box(v)
    # Boundary events must touch the actual attached activity border.
    for v in p['nodes']:
        if v['attach']:
            a=nodes[p['id']+'_'+v['attach']]['box'];v['box']=(a[0]+a[2]-50,a[1]+a[3]-20,40,40)
    def intersects(a,b,margin=4):
        return a[0]<b[0]+b[2]+margin and a[0]+a[2]>b[0]-margin and a[1]<b[1]+b[3]+margin and a[1]+a[3]>b[1]-margin
    body_boxes=[v['box'] for v in p['nodes']];label_boxes=[]
    for v in p['nodes']:
        if v['kind'] not in ('start','timerStart','end','xor','boundaryError','boundaryTimer','catchMessage'):continue
        x,y,w,h=v['box'];lw=170;lh=len(wrap(v['label'],22))*17+8
        candidates=[(x+w/2-lw/2,y+h+8,lw,lh),(x+w/2-lw/2,y-lh-8,lw,lh),
          (x-lw-12,y+h/2-lh/2,lw,lh),(x+w+12,y+h/2-lh/2,lw,lh)]
        candidates.extend((x+w/2-lw/2,y+h+8+k*20,lw,lh) for k in range(1,5))
        chosen=next((r for r in candidates if r[0]>55 and r[1]>92 and not any(intersects(r,b) for b in body_boxes+label_boxes)),None)
        if chosen is None:raise RuntimeError('No clear node label '+v['id'])
        v['labelBox']=chosen;label_boxes.append(chosen)
    obstacles=body_boxes+label_boxes
    occupied=obstacles.copy()
    for edge in p['edges']:
        edge['points']=route(nodes[edge['source']]['box'],nodes[edge['target']]['box'],obstacles)
        if edge['label']:
            candidates=[];lh=len(wrap(edge['label'],20))*16+5;lw=144
            for a,b in zip(edge['points'],edge['points'][1:]):
                length=abs(a[0]-b[0])+abs(a[1]-b[1]);cx=(a[0]+b[0])/2;cy=(a[1]+b[1])/2
                for offset in (-18,-38,20,40,-65,65):
                    r=(cx-lw/2,cy+offset-lh/2,lw,lh)
                    if r[0]>60 and r[1]>92 and not any(intersects(r,box) for box in occupied):candidates.append((length-abs(offset),r))
            if not candidates:raise RuntimeError('No clear flow label '+edge['id'])
            r=max(candidates,key=lambda c:c[0])[1];edge['labelBox']=r;edge['labelPoint']=(r[0]+r[2]/2,r[1]+14);occupied.append(r)
    maxx=max([v['box'][0]+v['box'][2] for v in p['nodes']]+[a[0] for e in p['edges'] for a in e['points']]+[r[0]+r[2] for r in occupied])
    maxy=max([v['box'][1]+v['box'][3]+70 for v in p['nodes']]+[a[1]+30 for e in p['edges'] for a in e['points']]+[r[1]+r[3] for r in occupied])
    p['width']=maxx+100;p['height']=max(maxy,100+len(p['lanes'])*230)
    p['canvasHeight']=p['height']+(140 if p['siem'] else 0)

def definitions():
    root=E.Element('{'+NS['bpmn']+'}definitions',nsmap=NS,id='GuardAuditDefinitions',
        targetNamespace='https://guard.local/audit/bpmn',exporter='Guard audit documentation',exporterVersion='1.0')
    child(root,'bpmn','message',id='Message_CEF',name='Syslog TLS CEF событие с EventId')
    for p in PROCESSES:
        for i,ed in enumerate(p['edges']):ed['id']=p['id']+'_F'+str(i+1)
        proc=child(root,'bpmn','process',id=p['id'],name=p['title'],isExecutable='false')
        child(proc,'bpmn','documentation',p['doc'])
        ls=child(proc,'bpmn','laneSet',id=p['id']+'_Lanes')
        for i,name in enumerate(p['lanes']):
            lane=child(ls,'bpmn','lane',id=p['id']+'_Lane'+str(i),name=name)
            for v in p['nodes']:
                if v['lane']==i:child(lane,'bpmn','flowNodeRef',v['id'])
        for v in p['nodes']:
            kind={'start':'startEvent','timerStart':'startEvent','end':'endEvent','service':'serviceTask','user':'userTask',
                'send':'sendTask','call':'callActivity','xor':'exclusiveGateway','boundaryTimer':'boundaryEvent','boundaryError':'boundaryEvent','catchMessage':'intermediateCatchEvent'}[v['kind']]
            attrs={'id':v['id'],'name':v['label']}
            if v['call']:attrs['calledElement']=v['call']
            if v['attach']:attrs.update(attachedToRef=p['id']+'_'+v['attach'],cancelActivity='true')
            for ed in p['edges']:
                if ed['source']==v['id'] and ed['default'] and kind=='exclusiveGateway':attrs['default']=ed['id']
            el=child(proc,'bpmn',kind,**attrs)
            child(el,'bpmn','documentation',v['doc'] or v['label'])
            for ed in p['edges']:
                if ed['target']==v['id']:child(el,'bpmn','incoming',ed['id'])
            for ed in p['edges']:
                if ed['source']==v['id']:child(el,'bpmn','outgoing',ed['id'])
            if v['kind'] in ('timerStart','boundaryTimer'):
                td=child(el,'bpmn','timerEventDefinition',id=v['id']+'_Timer')
                t=child(td,'bpmn','timeDate' if v['kind']=='boundaryTimer' else 'timeCycle',v['timer'])
                t.set('{'+NS['xsi']+'}type','bpmn:tFormalExpression')
            if v['kind']=='boundaryError':child(el,'bpmn','errorEventDefinition',id=v['id']+'_Error')
            if v['kind']=='catchMessage':child(el,'bpmn','messageEventDefinition',id=v['id']+'_Message')
        for ed in p['edges']:
            el=child(proc,'bpmn','sequenceFlow',id=ed['id'],sourceRef=ed['source'],targetRef=ed['target'],name=ed['label'])
            src=next(v for v in p['nodes'] if v['id']==ed['source'])
            out=[e for e in p['edges'] if e['source']==src['id']]
            if src['kind']=='xor' and len(out)>1 and not ed['default']:
                c=child(el,'bpmn','conditionExpression',ed['label'] or 'Условие согласно описанию процесса')
                c.set('{'+NS['xsi']+'}type','bpmn:tFormalExpression')
        if p['siem']:
            coll=child(root,'bpmn','collaboration',id=p['id']+'_Collaboration')
            child(coll,'bpmn','participant',id=p['id']+'_Guard',name='Guard',processRef=p['id'])
            child(coll,'bpmn','participant',id=p['id']+'_SIEM',name='RuSIEM внешняя система')
            send=next(v for v in p['nodes'] if v['kind']=='send')
            child(coll,'bpmn','messageFlow',id=p['id']+'_CEF',sourceRef=send['id'],targetRef=p['id']+'_SIEM',messageRef='Message_CEF',name='Syslog TLS CEF')
    return root

def add_di(root,p):
    dia=child(root,'bpmndi','BPMNDiagram',id=p['id']+'_Diagram',name=p['title'])
    plane=child(dia,'bpmndi','BPMNPlane',id=p['id']+'_Plane',bpmnElement=p['id']+'_Collaboration' if p['siem'] else p['id'])
    def shape(id,bounds,horizontal=None,label=None):
        attrs={'id':id+'_DI','bpmnElement':id}
        if horizontal is not None:attrs['isHorizontal']='true' if horizontal else 'false'
        sh=child(plane,'bpmndi','BPMNShape',**attrs);child(sh,'dc','Bounds',x=bounds[0],y=bounds[1],width=bounds[2],height=bounds[3])
        if label:
            lb=child(sh,'bpmndi','BPMNLabel');child(lb,'dc','Bounds',x=label[0],y=label[1],width=label[2],height=label[3])
        return sh
    if p['siem']:shape(p['id']+'_Guard',(10,85,p['width']-20,p['height']-70),True)
    for i,_ in enumerate(p['lanes']):shape(p['id']+'_Lane'+str(i),(35 if p['siem'] else 10,100+i*230,p['width']-(45 if p['siem'] else 20),230),True)
    for v in p['nodes']:
        x,y,w,h=v['box'];label=None
        if v.get('labelBox'):label=v['labelBox']
        sh=shape(v['id'],v['box'],label=label)
        if v['kind']=='xor':sh.set('isMarkerVisible','true')
        if v['kind']=='call':sh.set('isExpanded','false')
    for ed in p['edges']:
        edge=child(plane,'bpmndi','BPMNEdge',id=ed['id']+'_DI',bpmnElement=ed['id'])
        for x,y in ed['points']:child(edge,'di','waypoint',x=x,y=y)
        if ed['label']:
            x,y,w,h=ed['labelBox'];lb=child(edge,'bpmndi','BPMNLabel');child(lb,'dc','Bounds',x=x,y=y,width=w,height=h)
    if p['siem']:
        bounds=(35,p['height']+40,p['width']-70,70);shape(p['id']+'_SIEM',bounds,True)
        v=next(v for v in p['nodes'] if v['kind']=='send');x,y,w,h=v['box']
        pts=route(v['box'],(x+w/2-20,p['height']+40,40,1),[n['box'] for n in p['nodes']])
        p['messagePoints']=pts
        edge=child(plane,'bpmndi','BPMNEdge',id=p['id']+'_CEF_DI',bpmnElement=p['id']+'_CEF')
        for x,y in pts:child(edge,'di','waypoint',x=x,y=y)

def validate(root,path):
    schema=E.XMLSchema(E.parse(str(WORK/'schema/BPMN20.xsd')));schema.assertValid(root)
    elements={el.get('id'):el for el in root.iter() if el.get('id')}
    ids=[el.get('id') for el in root.iter() if el.get('id')]
    assert len(ids)==len(set(ids)), 'Duplicate XML IDs'
    for el in root.iter():
        for key in ('sourceRef','targetRef','attachedToRef','calledElement','bpmnElement','default','processRef','messageRef'):
            if el.get(key):assert el.get(key) in elements,(path,key,el.get(key))
    checks=[]
    for p in PROCESSES:
        incoming={v['id']:[] for v in p['nodes']};outgoing={v['id']:[] for v in p['nodes']}
        for ed in p['edges']:incoming[ed['target']].append(ed);outgoing[ed['source']].append(ed)
        for v in p['nodes']:
            if v['kind'] not in ('start','timerStart','boundaryError','boundaryTimer'):assert incoming[v['id']],v['id']
            if v['kind']!='end':assert outgoing[v['id']],v['id']
            if v['kind']=='end':assert not outgoing[v['id']],v['id']
            if v['kind'] not in ('xor','end'):assert len(outgoing[v['id']])==1,(v['id'],'implicit parallel split')
            if v['kind']=='xor' and len(outgoing[v['id']])>1:
                assert all(e['label'] for e in outgoing[v['id']]),v['id']
        reached=set();stack=[v['id'] for v in p['nodes'] if v['kind'] in ('start','timerStart')]
        while stack:
            cur=stack.pop()
            if cur in reached:continue
            reached.add(cur);stack.extend(e['target'] for e in outgoing[cur])
            stack.extend(v['id'] for v in p['nodes'] if v['attach'] and p['id']+'_'+v['attach']==cur)
        assert len(reached)==len(p['nodes']), (p['id'],'unreachable',set(incoming)-reached)
        checks.append({'process':p['id'],'nodes':len(p['nodes']),'flows':len(p['edges']),'reachable':len(reached)})
    return checks

def svg(p):
    root=E.Element('{'+SVG+'}svg',nsmap={None:SVG},viewBox=f"0 0 {p['width']} {p['canvasHeight']+70}",width=str(p['width']),height=str(p['canvasHeight']+70))
    def s(name,**attrs):return E.SubElement(root,'{'+SVG+'}'+name,**{k:str(v) for k,v in attrs.items()})
    s('rect',x=0,y=0,width=p['width'],height=p['canvasHeight']+70,fill='white')
    def text(label,x,y,size=15,anchor='middle',color='#16313a',width=21):
        lines=wrap(label,width)
        el=s('text',x=x,y=y,fill=color,**{'font-family':'Arial, sans-serif','font-size':size,'text-anchor':anchor})
        for i,line in enumerate(lines):E.SubElement(el,'{'+SVG+'}tspan',x=str(x),dy=str(0 if i==0 else size+3)).text=line
    text(p['id']+'  '+p['title'],28,35,23,'start',width=110)
    text('Описательная BPMN 2.0  |  Guard  |  20 октября 2026',28,63,13,'start',width=150)
    for i,name in enumerate(p['lanes']):
        y=100+i*230;s('rect',x=10,y=y,width=p['width']-20,height=230,fill='#f4f7f8' if i%2==0 else '#ffffff',stroke='#c5d2d7',**{'stroke-width':1})
        el=s('text',x=32,y=y+115,fill='#3b5965',transform=f'rotate(-90 32 {y+115})',**{'text-anchor':'middle','font-family':'Arial','font-size':14});el.text=name
    for edge in p['edges']:
        pts=edge['points'];s('polyline',points=' '.join(f'{x},{y}' for x,y in pts),fill='none',stroke='#526b77',**{'stroke-width':1.6})
        a,b=pts[-2:];angle=math.atan2(b[1]-a[1],b[0]-a[0]);arr=[b,(b[0]-10*math.cos(angle-.4),b[1]-10*math.sin(angle-.4)),(b[0]-10*math.cos(angle+.4),b[1]-10*math.sin(angle+.4))]
        s('polygon',points=' '.join(f'{x},{y}' for x,y in arr),fill='#526b77')
        if edge['label']:
            x,y=edge['labelPoint'];r=edge['labelBox'];s('rect',x=r[0],y=r[1],width=r[2],height=r[3],fill='white',opacity='.95');text(edge['label'],x,y,12,width=20)
    for v in p['nodes']:
        x,y,w,h=v['box'];cx=x+w/2;cy=y+h/2;k=v['kind']
        if k in ('start','timerStart','end','boundaryError','boundaryTimer','catchMessage'):
            s('circle',cx=cx,cy=cy,r=w/2,fill='white',stroke='#274d59',**{'stroke-width':3.5 if k=='end' else 1.8})
            if k.startswith('boundary') or k=='catchMessage':s('circle',cx=cx,cy=cy,r=w/2-4,fill='none',stroke='#274d59')
            if k=='catchMessage':
                s('rect',x=cx-11,y=cy-8,width=22,height=16,fill='none',stroke='#274d59');s('path',d=f'M {cx-11} {cy-8} L {cx} {cy+1} L {cx+11} {cy-8}',fill='none',stroke='#274d59')
            if k in ('timerStart','boundaryTimer'):
                s('circle',cx=cx,cy=cy,r=12,fill='none',stroke='#274d59');s('path',d=f'M {cx} {cy-8} V {cy} L {cx+7} {cy+4}',fill='none',stroke='#274d59')
            if k=='boundaryError':s('path',d=f'M {cx+4} {cy-11} L {cx-6} {cy+1} L {cx+2} {cy} L {cx-3} {cy+12} L {cx+9} {cy-3} L {cx+2} {cy-2} Z',fill='#274d59')
            lx,ly,lw,lh=v['labelBox'];text(v['label'],lx+lw/2,ly+14,14,width=22)
        elif k=='xor':
            s('polygon',points=f'{cx},{y} {x+w},{cy} {cx},{y+h} {x},{cy}',fill='#fff8e8',stroke='#8b7341',**{'stroke-width':1.8})
            s('path',d=f'M {cx-8} {cy-8} L {cx+8} {cy+8} M {cx+8} {cy-8} L {cx-8} {cy+8}',stroke='#8b7341',**{'stroke-width':2})
            lx,ly,lw,lh=v['labelBox'];text(v['label'],lx+lw/2,ly+14,14,width=22)
        else:
            fill={'user':'#eef5ff','call':'#e7f1ee','send':'#edf5ff'}.get(k,'#ffffff')
            s('rect',x=x,y=y,width=w,height=h,rx=9,fill=fill,stroke='#315b67',**{'stroke-width':3 if k=='call' else 1.6})
            ix,iy=x+14,y+14
            if k=='service':
                for ang in range(0,360,45):
                    a=math.radians(ang);s('line',x1=ix+5*math.cos(a),y1=iy+5*math.sin(a),x2=ix+9*math.cos(a),y2=iy+9*math.sin(a),stroke='#315b67',**{'stroke-width':3})
                s('circle',cx=ix,cy=iy,r=5,fill='white',stroke='#315b67');s('circle',cx=ix,cy=iy,r=2,fill='#315b67')
            elif k=='user':
                s('circle',cx=ix,cy=iy-5,r=4,fill='none',stroke='#315b67');s('path',d=f'M {ix-7} {iy+7} Q {ix-7} {iy} {ix} {iy} Q {ix+7} {iy} {ix+7} {iy+7} Z',fill='none',stroke='#315b67')
            elif k=='send':
                s('rect',x=ix-9,y=iy-6,width=18,height=12,fill='#315b67',stroke='#315b67');s('path',d=f'M {ix-9} {iy-6} L {ix} {iy+1} L {ix+9} {iy-6}',fill='none',stroke='white')
            elif k=='call':
                s('rect',x=cx-6,y=y+h-15,width=12,height=12,fill='white',stroke='#315b67');s('path',d=f'M {cx-4} {y+h-9} H {cx+4} M {cx} {y+h-13} V {y+h-5}',stroke='#315b67')
            lines=wrap(v['label'],20);text(v['label'],cx,cy-(len(lines)-1)*7.5+4,12,width=20)
    if p['siem']:
        s('rect',x=35,y=p['height']+40,width=p['width']-70,height=70,fill='#f1f5fa',stroke='#516a79');text('RuSIEM  Внешняя система  Приём и разбор события подтверждаются отдельно',p['width']/2,p['height']+81,15,width=120)
        pts=p['messagePoints'];s('polyline',points=' '.join(f'{x},{y}' for x,y in pts),fill='none',stroke='#6e7e94',**{'stroke-width':1.6,'stroke-dasharray':'7 5'})
        s('circle',cx=pts[0][0],cy=pts[0][1],r=4,fill='white',stroke='#6e7e94')
        a,b=pts[-2:];angle=math.atan2(b[1]-a[1],b[0]-a[0])
        s('polyline',points=f'{b[0]-10*math.cos(angle-.4)},{b[1]-10*math.sin(angle-.4)} {b[0]},{b[1]} {b[0]-10*math.cos(angle+.4)},{b[1]-10*math.sin(angle+.4)}',fill='none',stroke='#6e7e94',**{'stroke-width':1.6})
    text('Шестерёнка автоматическая задача   Человек действие Root   Конверт сообщение   Толстая рамка вызов процесса   X исключающий шлюз',28,p['canvasHeight']+45,12,'start',width=190)
    return E.tostring(root,encoding='utf-8',xml_declaration=True,pretty_print=True)

def atlas():
    pdfmetrics.registerFont(TTFont('GuardArial','C:/Windows/Fonts/arial.ttf'))
    path=OUT/'Guard_Audit_BPMN_Atlas.pdf';c=canvas.Canvas(str(path),pagesize=landscape(A3));pagew,pageh=landscape(A3)
    c.setTitle('Guard Аудит Атлас BPMN 2.0');c.setAuthor('Guard')
    def drawtext(text,x,y,size,width=20):
        lines=wrap(text,width);c.setFont('GuardArial',size)
        for i,line in enumerate(lines):c.drawCentredString(x,y-i*(size+3),line)
    for p in PROCESSES:
        pagew,pageh=landscape(A2 if p['width']>1900 else A3);c.setPageSize((pagew,pageh))
        scale=min((pagew-48)/p['width'],(pageh-92)/p['canvasHeight'])
        c.setFillColor(HexColor('#16313a'));c.setFont('GuardArial',16);c.drawString(24,pageh-26,p['id']+'  '+p['title'])
        c.setFont('GuardArial',8);c.drawString(24,pageh-42,'BPMN 2.0  |  Описательная модель текущей реализации  |  20 октября 2026')
        c.saveState();c.translate(24,pageh-54);c.scale(scale,-scale)
        def txt(label,x,y,size=14,width=20):
            c.saveState();c.translate(x,y);c.scale(1,-1);c.setFillColor(HexColor('#16313a'));drawtext(label,0,0,size,width);c.restoreState()
        for i,name in enumerate(p['lanes']):
            y=100+i*230;c.setFillColor(HexColor('#f4f7f8') if i%2==0 else HexColor('#ffffff'));c.setStrokeColor(HexColor('#c5d2d7'));c.setLineWidth(1);c.rect(10,y,p['width']-20,230,fill=1,stroke=1)
            c.saveState();c.translate(32,y+115);c.rotate(90);c.scale(-1,1);c.setFont('GuardArial',14);c.setFillColor(HexColor('#3b5965'));c.drawCentredString(0,0,name);c.restoreState()
        for ed in p['edges']:
            pts=ed['points'];c.setStrokeColor(HexColor('#526b77'));c.setLineWidth(1.6);pathobj=c.beginPath();pathobj.moveTo(*pts[0])
            for pt in pts[1:]:pathobj.lineTo(*pt)
            c.drawPath(pathobj)
            a,b=pts[-2:];angle=math.atan2(b[1]-a[1],b[0]-a[0]);pa=c.beginPath();pa.moveTo(*b);pa.lineTo(b[0]-10*math.cos(angle-.4),b[1]-10*math.sin(angle-.4));pa.lineTo(b[0]-10*math.cos(angle+.4),b[1]-10*math.sin(angle+.4));pa.close();c.setFillColor(HexColor('#526b77'));c.drawPath(pa,fill=1,stroke=0)
            if ed['label']:
                x,y=ed['labelPoint'];r=ed['labelBox'];c.setFillColor(HexColor('#ffffff'));c.rect(*r,fill=1,stroke=0);txt(ed['label'],x,y,12,20)
        for v in p['nodes']:
            x,y,w,h=v['box'];cx=x+w/2;cy=y+h/2;k=v['kind'];c.setStrokeColor(HexColor('#315b67'));c.setFillColor(HexColor('#ffffff'));c.setLineWidth(3 if k in ('end','call') else 1.6)
            if k in ('start','end','timerStart','boundaryTimer','boundaryError','catchMessage'):
                c.circle(cx,cy,w/2,fill=1,stroke=1)
                if k.startswith('boundary') or k=='catchMessage':c.circle(cx,cy,w/2-4,fill=0,stroke=1)
                if k=='catchMessage':
                    c.rect(cx-11,cy-8,22,16,fill=0,stroke=1);c.line(cx-11,cy-8,cx,cy+1);c.line(cx,cy+1,cx+11,cy-8)
                if k in ('timerStart','boundaryTimer'):
                    c.setLineWidth(1);c.circle(cx,cy,12);c.line(cx,cy-8,cx,cy);c.line(cx,cy,cx+7,cy+4)
                if k=='boundaryError':
                    pa=c.beginPath();pa.moveTo(cx+4,cy-11)
                    for px,py in [(cx-6,cy+1),(cx+2,cy),(cx-3,cy+12),(cx+9,cy-3),(cx+2,cy-2)]:pa.lineTo(px,py)
                    pa.close();c.setFillColor(HexColor('#315b67'));c.drawPath(pa,fill=1,stroke=0)
                lx,ly,lw,lh=v['labelBox'];txt(v['label'],lx+lw/2,ly+14,14,22)
            elif k=='xor':
                pa=c.beginPath();pa.moveTo(cx,y);pa.lineTo(x+w,cy);pa.lineTo(cx,y+h);pa.lineTo(x,cy);pa.close();c.setFillColor(HexColor('#fff8e8'));c.drawPath(pa,fill=1,stroke=1);c.line(cx-8,cy-8,cx+8,cy+8);c.line(cx+8,cy-8,cx-8,cy+8);lx,ly,lw,lh=v['labelBox'];txt(v['label'],lx+lw/2,ly+14,14,22)
            else:
                c.setFillColor(HexColor({'user':'#eef5ff','call':'#e7f1ee','send':'#edf5ff'}.get(k,'#ffffff')));c.roundRect(x,y,w,h,9,fill=1,stroke=1)
                ix,iy=x+14,y+14;c.setLineWidth(1)
                if k=='service':
                    for ang in range(0,360,45):
                        a=math.radians(ang);c.line(ix+5*math.cos(a),iy+5*math.sin(a),ix+9*math.cos(a),iy+9*math.sin(a))
                    c.circle(ix,iy,5,fill=0);c.circle(ix,iy,2,fill=0)
                elif k=='user':
                    c.circle(ix,iy-5,4,fill=0);pa=c.beginPath();pa.moveTo(ix-7,iy+7);pa.curveTo(ix-7,iy,ix+7,iy,ix+7,iy+7);pa.close();c.drawPath(pa)
                elif k=='send':
                    c.setFillColor(HexColor('#315b67'));c.rect(ix-9,iy-6,18,12,fill=1,stroke=1);c.setStrokeColor(HexColor('#ffffff'));c.line(ix-9,iy-6,ix,iy+1);c.line(ix,iy+1,ix+9,iy-6)
                elif k=='call':
                    c.rect(cx-6,y+h-15,12,12,fill=0);c.line(cx-4,y+h-9,cx+4,y+h-9);c.line(cx,y+h-13,cx,y+h-5)
                txt(v['label'],cx,cy-(len(wrap(v['label'],20))-1)*7.5+4,12,20)
        if p['siem']:
            c.setStrokeColor(HexColor('#516a79'));c.setFillColor(HexColor('#f1f5fa'));c.rect(35,p['height']+40,p['width']-70,70,fill=1,stroke=1);txt('RuSIEM  Внешняя система  Приём подтверждается отдельно',p['width']/2,p['height']+81,15,120)
            c.setDash(7,5);pts=p['messagePoints'];pa=c.beginPath();pa.moveTo(*pts[0])
            for pt in pts[1:]:pa.lineTo(*pt)
            c.drawPath(pa);c.setDash();c.circle(*pts[0],4,fill=0)
            a,b=pts[-2:];angle=math.atan2(b[1]-a[1],b[0]-a[0]);c.line(b[0],b[1],b[0]-10*math.cos(angle-.4),b[1]-10*math.sin(angle-.4));c.line(b[0],b[1],b[0]-10*math.cos(angle+.4),b[1]-10*math.sin(angle+.4))
        c.restoreState();c.setFillColor(HexColor('#3b5965'));c.setFont('GuardArial',8);c.drawString(24,27,'Шестерёнка автоматическая задача  Человек действие Root  Конверт сообщение  Толстая рамка вызов процесса  X шлюз')
        c.drawRightString(pagew-24,27,p['id']+' / '+str(len(PROCESSES))+' процессов');c.showPage()
    c.save()

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    for p in PROCESSES:prepare(p)
    root=definitions()
    for p in PROCESSES:add_di(root,p)
    checks=validate(root,'master')
    E.ElementTree(root).write(str(OUT/'Guard_Audit_Master.bpmn'),encoding='utf-8',xml_declaration=True,pretty_print=True)
    for p in PROCESSES:
        one=definitions();add_di(one,p);validate(one,p['id'])
        E.ElementTree(one).write(str(OUT/(p['id']+'.bpmn')),encoding='utf-8',xml_declaration=True,pretty_print=True)
        (OUT/(p['id']+'.svg')).write_bytes(svg(p))
    atlas()
    (OUT/'model-data.json').write_text(json.dumps(PROCESSES,ensure_ascii=False,indent=2),encoding='utf-8')
    (WORK/'bpmn-validation.json').write_text(json.dumps({'xsd':'OMG BPMN 2.0.2 BPMN20.xsd','checks':checks,'executable':False},ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'processes':len(PROCESSES),'nodes':sum(x['nodes'] for x in checks),'flows':sum(x['flows'] for x in checks),'validation':'XSD and references and reachability passed'},ensure_ascii=False))

if __name__=='__main__':main()
