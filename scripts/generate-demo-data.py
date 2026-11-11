"""Generates the demo data (CSV, import-contract format) from the official Liwa University Catalog 2025-2026, and checks it.

    python -I scripts/generate-demo-data.py src/Liwa.Infrastructure/Seeding/Demo

Real (from docs/reference/University_Catalog_2025_2026.pdf; descriptions via scripts/extract_catalog.py):
  courses, credits, prerequisites, descriptions, the three programs, their requirement groups and the suggested
  Year/Semester sequence. Page numbers below are PDF pages; every course and plan row carries a `source` column.
Illustrative (until the Registrar supplies real data): Arabic course titles (machine-drafted), offering pattern
  (every term: the catalog doesn't say), the Spring 2027 timetable, students and transcripts. One transcript
  (student.hanieh) is a real record supplied by the project owner, with the name, ID and email replaced.
Where the curriculum table and the course descriptions disagree, the curriculum table of the program wins and the
  difference is printed (and recorded in docs/assumptions.md).
"""
import csv
import json
import os
import re
import sys

OUT = sys.argv[1]
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DESCRIPTIONS = json.load(open(os.path.join(ROOT, 'docs', 'reference', 'catalog-2025-2026-courses.json'), encoding='utf-8'))
EVERY_TERM = 'Fall;Spring;Summer'  # the catalog has no offering pattern; registrar question (assumptions.md)
CATALOG = 'Catalog 2025-2026'

GE, BA, IT, MC = 'General Education', 'Business Administration', 'Information Technology', 'Mass Communication'
C = {}  # code -> dict(title, ar, credits, dept, pre, page)


def c(code, title, ar, dept, pre='', page=0, credits=3):
    assert code not in C, code
    C[code] = dict(title=title, ar=ar, credits=credits, dept=dept, pre=pre, page=page)


# Alternatives the catalog lists with "/": students take one of them (CIT101 replaces CIT100 from Fall 2025;
# GEN101 is for non-Arabic speakers). Prerequisites naming one accept the other.
IT_INTRO = '(CIT100 OR CIT101)'
ARABIC = '(GEN100 OR GEN101)'

# ---- General education (identical in all three programs; BBA-BIT p.130, BIT p.194, BMC p.284)
c('CIT100', 'Introduction to Information Technology', 'مقدمة في تقنية المعلومات', GE, '', 130)
c('CIT101', 'Introduction to Generative AI', 'مقدمة في الذكاء الاصطناعي التوليدي', GE, '', 130)
c('ENG100', 'English I', 'اللغة الإنجليزية 1', GE, '', 130)  # admission: English proficiency test (not a course)
c('GEN100', 'Communication Skills in Arabic', 'مهارات الاتصال باللغة العربية', GE, '', 130)
c('GEN101', 'Communication Skills in Arabic for Non-Arabic Speakers', 'مهارات الاتصال باللغة العربية لغير الناطقين بها', GE, '', 130)
c('GEN102', 'Islamic Culture', 'الثقافة الإسلامية', GE, '', 130)
c('GEN105', 'Emirates Culture and Society', 'ثقافة ومجتمع الإمارات', GE, '', 130)
c('ENG104', 'English II', 'اللغة الإنجليزية 2', GE, 'ENG100', 130)
c('GEN103', 'Logic and Critical Thinking', 'المنطق والتفكير الناقد', GE, 'ENG100', 130)
c('GEN201', 'Psychology', 'علم النفس', GE, 'ENG100', 130)
c('GEN302', 'Fundamentals of Innovation and Entrepreneurship', 'أساسيات الابتكار وريادة الأعمال', GE, 'ENG100', 130)
c('GEN304', 'Ethics', 'الأخلاقيات', GE, 'ENG100', 130)
c('MTH105', 'Mathematics and Statistics', 'الرياضيات والإحصاء', GE, '', 130)

# ---- BBA in Business Information Technology: core (p.130) and major (p.131)
c('BUS100', 'Introduction to Management', 'مقدمة في الإدارة', BA, '', 130)
c('ACC106', 'Accounting Principles I', 'مبادئ المحاسبة 1', BA, '', 130)
c('MKT200', 'Introduction to Marketing', 'مقدمة في التسويق', BA, 'BUS100', 130)
c('ACC200', 'Accounting Principles II', 'مبادئ المحاسبة 2', BA, 'ACC106', 130)
c('HRM201', 'Introduction to Human Resource Management', 'مقدمة في إدارة الموارد البشرية', BA, '', 130)
c('BUS201', 'Microeconomics', 'الاقتصاد الجزئي', BA, '', 130)
c('BUS203', 'Organizational Behavior', 'السلوك التنظيمي', BA, 'BUS100', 130)
c('BIT203', 'Management Information Systems', 'نظم المعلومات الإدارية', BA, IT_INTRO, 130)
c('MTH203', 'Business Statistics', 'إحصاء الأعمال', BA, 'MTH105', 130)
c('BUS301', 'Macroeconomics', 'الاقتصاد الكلي', BA, 'BUS201', 130)
c('BAF301', 'Introduction to Financial Management', 'مقدمة في الإدارة المالية', BA, 'ACC106', 130)
c('BUS306', 'Quantitative Business Analysis', 'التحليل الكمي للأعمال', BA, 'MTH203', 130)
c('BUS307', 'Research Methods', 'مناهج البحث', BA, 'BUS306', 130)
c('BUS309', 'Operations Management', 'إدارة العمليات', BA, 'BUS306', 130)
c('BUS409', 'Strategic Management', 'الإدارة الاستراتيجية', BA, 'BUS417', 130)
c('BUS417', 'International Business Management', 'إدارة الأعمال الدولية', BA, 'MKT200 AND BUS301', 130)
c('BIT201', 'Introduction to Computer Programming', 'مقدمة في برمجة الحاسوب', IT, IT_INTRO, 131)
c('BIT205', 'Object Oriented Programming', 'البرمجة كائنية التوجه', IT, 'BIT201', 131)
c('BIT207', 'Introduction to Web Development', 'مقدمة في تطوير الويب', IT, IT_INTRO, 131)
c('BUS302', 'E-Business', 'الأعمال الإلكترونية', BA, 'BIT203', 131)
c('BIT305', 'Database Management Systems', 'نظم إدارة قواعد البيانات', IT, IT_INTRO, 131)
c('BIT405', 'Business Intelligence', 'ذكاء الأعمال', IT, 'BIT305', 131)
c('BIT407', 'Object Oriented Systems Analysis and Design', 'تحليل وتصميم النظم كائنية التوجه', IT, 'BIT205', 131)
c('BIT413', 'Data Communications and Networking', 'اتصالات البيانات والشبكات', IT, IT_INTRO, 131)
c('BIT415', 'Graduation Project', 'مشروع التخرج', IT, 'CREDITS(99) AND BIT407', 131)
c('BIT430', 'Information Security', 'أمن المعلومات', IT, 'BIT413', 131)
c('BIT417', 'Advanced Web Development', 'تطوير الويب المتقدم', IT, 'BIT207', 131)
c('BIT422', 'Advanced Database Management Systems', 'نظم إدارة قواعد البيانات المتقدمة', IT, 'BIT305', 131)
c('BIT419', 'Internship', 'التدريب العملي', IT, 'CREDITS(99)', 131)
c('BUS421', 'Entrepreneurship and Small Business Management', 'ريادة الأعمال وإدارة المشروعات الصغيرة', BA, 'BUS203', 131)
c('BAF403', 'Financial Institutions and Markets', 'المؤسسات والأسواق المالية', BA, 'BAF301', 131)
c('MKT301', 'Consumer Behavior', 'سلوك المستهلك', BA, 'MKT200', 131)
c('BUS411', 'Change Management', 'إدارة التغيير', BA, 'BUS203', 131)

# ---- Bachelor of Information Technology (BIT) — General Track: core (p.194), electives and track (p.195)
c('CIT112', 'Introduction to Computer Programming', 'مقدمة في برمجة الحاسوب', IT, '', 194)
c('CIT121', 'Introduction to Computer Architecture', 'مقدمة في معمارية الحاسوب', IT, '', 194)
c('CIT122', 'Introduction to Database Management Systems', 'مقدمة في نظم إدارة قواعد البيانات', IT, 'CIT112', 194)
c('CIT123', 'Computer Programming and Problem Solving', 'برمجة الحاسوب وحل المشكلات', IT, 'CIT112', 194)
c('CIT231', 'Introduction to Computer Networks', 'مقدمة في شبكات الحاسوب', IT, 'CIT121', 194)
c('CIT232', 'Introduction to Operating Systems', 'مقدمة في نظم التشغيل', IT, 'CIT121', 194)
c('CIT241', 'Network Security', 'أمن الشبكات', IT, 'CIT231', 194)
c('CIT242', 'System Analysis and Design', 'تحليل النظم وتصميمها', IT, 'CIT122', 194)
c('CIT244', 'Database Systems', 'نظم قواعد البيانات', IT, 'CIT122', 194)
c('CIT234', 'Discrete Mathematics', 'الرياضيات المتقطعة', IT, 'MTH105', 194)
c('CIT353', 'Web Development', 'تطوير الويب', IT, 'CIT123', 194)
c('CIT354', 'Object-Oriented Programming', 'البرمجة كائنية التوجه', IT, 'CIT123', 194)
c('BUS407', 'Project Management', 'إدارة المشاريع', BA, 'BIT203', 194)
c('CIT362', 'Information Security Management', 'إدارة أمن المعلومات', IT, 'CIT241', 194)
c('BIM200', 'Linear Algebra', 'الجبر الخطي', GE, 'MTH105', 194)
c('CIT483', 'Internship', 'التدريب العملي', IT, 'CREDITS(75)', 194)
c('CIT472', 'User Interface Design', 'تصميم واجهات المستخدم', IT, 'CIT354', 194)
c('MTH110', 'Calculus I', 'التفاضل والتكامل 1', GE, '', 194)
c('CIT478', 'Mobile Applications Development', 'تطوير تطبيقات الهاتف المحمول', IT, 'CIT472', 194)
c('CIT473', 'Introduction to Artificial Intelligence', 'مقدمة في الذكاء الاصطناعي', IT, 'CREDITS(48)', 194)
c('CIT484', 'Senior Graduation Project I', 'مشروع التخرج 1', IT, 'CREDITS(90)', 194)
c('CIT485', 'Senior Graduation Project II', 'مشروع التخرج 2', IT, 'CIT484', 194)
c('CIT474', 'Internet of Things', 'إنترنت الأشياء', IT, 'CIT231 AND CREDITS(81)', 195)
c('CIT487', 'Cloud and Edge Computing', 'الحوسبة السحابية والطرفية', IT, 'CIT231 AND CREDITS(81)', 195)
c('CIT477', 'Natural Language Processing', 'معالجة اللغات الطبيعية', IT, 'CIT473 AND CREDITS(81)', 195)
c('CIT470', 'Data Mining', 'التنقيب في البيانات', IT, 'CIT473 AND CREDITS(81)', 195)
c('CIT233', 'Software Development Process', 'عملية تطوير البرمجيات', IT, 'CIT112', 195)
c('CIT243', 'Network Administration', 'إدارة الشبكات', IT, 'CIT231', 195)
c('CIT352', 'Distributed Information Management', 'إدارة المعلومات الموزعة', IT, 'CIT231', 195)
c('CIT365', 'Advanced Database Design', 'تصميم قواعد البيانات المتقدم', IT, 'CIT244', 195)
c('CIT481', 'Contemporary Issues in IT', 'قضايا معاصرة في تقنية المعلومات', IT, 'CREDITS(81)', 195)

# ---- Bachelor of Mass Communication Concentration in Public Relations (p.284-285; taught in Arabic)
c('MCM101', 'Introduction to Digital Media', 'مدخل إلى الإعلام الرقمي', MC, '', 284)
c('MCM102', 'Introduction to Public Relations', 'مدخل إلى العلاقات العامة', MC, '', 284)
c('MCM121', 'Introduction to Advertising', 'مدخل إلى الإعلان', MC, '', 284)
c('MCM123', 'Media Theories', 'نظريات الإعلام', MC, '', 284)
c('MCM201', 'Media in the UAE', 'الإعلام في دولة الإمارات', MC, 'GEN105', 284)
c('MCM202', 'Media Research Methods', 'مناهج البحث الإعلامي', MC, 'MCM123', 284)
c('MCM203', 'Digital Photography', 'التصوير الرقمي', MC, 'MCM101', 284)
c('MCM301', 'International Communication', 'الاتصال الدولي', MC, '', 284)
c('MCM210', 'Geopolitics', 'الجغرافيا السياسية', MC, '', 285)
c('MCM211', 'Consumer Behavior', 'سلوك المستهلك', MC, '', 285)
c('MCM212', 'The Art of Persuasion', 'فن الإقناع', MC, '', 285)
c('MCM213', 'Social Psychology', 'علم النفس الاجتماعي', MC, 'GEN201', 285)
c('MCM122', 'Professional Writing in Arabic', 'الكتابة المهنية باللغة العربية', MC, ARABIC, 285)
c('MCM204', 'Introduction to Media and Artificial Intelligence', 'مقدمة في الإعلام والذكاء الاصطناعي', MC, '', 285)
c('MCM221', 'Rhetorical Communication', 'الاتصال البلاغي', MC, '', 285)
c('MCM222', 'Social Marketing', 'التسويق الاجتماعي', MC, 'MCM121', 285)
c('MCM223', 'Public Opinion', 'الرأي العام', MC, '', 285)
c('MCM321', 'Arab Media', 'الإعلام العربي', MC, 'MCM201', 285)
c('MCM322', 'Media Translation', 'الترجمة الإعلامية', MC, 'ENG100', 285)
c('MCM401', 'Economics and Management of Media Organizations', 'اقتصاديات المؤسسات الإعلامية وإدارتها', MC, '', 285)
c('MCM421', 'Protocol and Etiquette', 'البروتوكول والإتيكيت', MC, '', 285)
c('MCM125', 'Introduction to Sociology', 'مدخل إلى علم الاجتماع', MC, '', 285)
c('MCM326', 'Modern Arab History', 'التاريخ العربي الحديث', MC, '', 285)
c('MCM327', 'Global Issues', 'قضايا عالمية', MC, '', 285)
c('MCM328', 'Modern Arabic Literature', 'الأدب العربي الحديث', MC, '', 285)
c('MCM329', 'Documentary Cinema', 'السينما الوثائقية', MC, '', 285)
c('PRM301', 'Public Relations Management', 'إدارة العلاقات العامة', MC, 'MCM102', 285)
c('PRM302', 'Writing for Public Relations', 'الكتابة للعلاقات العامة', MC, 'MCM122', 285)
c('PRM303', 'Production of Materials for Public Relations', 'إنتاج مواد العلاقات العامة', MC, 'MCM203', 285)
c('PRM321', 'International Public Relations', 'العلاقات العامة الدولية', MC, 'MCM301', 285)
c('PRM401', 'Case Studies in Public Relations', 'دراسات حالة في العلاقات العامة', MC, 'PRM301', 285)
c('PRM402', 'Public Relations Campaigns', 'حملات العلاقات العامة', MC, 'PRM301', 285)
c('PRM403', 'Online Public Relations', 'العلاقات العامة الرقمية', MC, 'MCM101', 285)
# "Passing 80 hours, including 12 hours of specialization courses" (p.285); the description adds PRM303 (p.377).
# The "12 hours of specialization" part can't be written as a rule yet (assumptions.md).
c('PRM410', 'Field Training in Public Relations', 'التدريب الميداني في العلاقات العامة', MC, 'CREDITS(80) AND PRM303', 285)
# "Graduation Semester" (p.285) / "Graduation Class + PRM410" (p.377): approximated as at most 15 credits left.
c('PRM422', 'Graduation Project in Public Relations', 'مشروع التخرج في العلاقات العامة', MC, 'PRM410 AND CREDITS(108)', 285)

for code, x in C.items():
    for ref in re.findall(r'[A-Z]{2,4}\d{3}', x['pre']):
        assert ref in C, (code, ref)

# ---- Cross-check against the Course Descriptions section (the curriculum table wins; differences are printed).
def codes_in(text):
    return set(re.findall(r'[A-Z]{2,4}\d{3}', re.sub(r'([A-Z]{2,4})\s+(\d{3})', r'\1\2', text.upper())))


DIFFS = []
for code, x in C.items():
    d = DESCRIPTIONS.get(code)
    if not d:
        DIFFS.append(f'{code}: not in Course Descriptions')
        continue
    ours, theirs = codes_in(x['pre']), codes_in(d['prerequisites'])
    if ours != theirs and not (ours >= {'CIT100'} and theirs == {'CIT100'}) and not (ours >= {'GEN100'} and theirs == {'GEN100'}):
        DIFFS.append(f"{code}: curriculum '{x['pre'] or 'None'}' vs description '{d['prerequisites'] or 'None'}' (p.{d['pdf_page']})")
for line in DIFFS:
    print('  differs:', line, file=sys.stderr)

# ---- Programs and their plans: requirement groups + the catalog's Year/Semester sequence (1..8).
# Slots like 'MAJ-ELEC' in a semester are elective/choice groups; they are used for suggested_semester only.
PROGRAMS = {
    'BBA-BIT': dict(
        name='Bachelor of Business Administration in Business Information Technology', ar='بكالوريوس إدارة الأعمال في تقنية معلومات الأعمال',
        college='College of Business Administration', pages='130-132',
        groups=[
            ('GEN-IT', 'Introduction to IT or Generative AI (choose one)', 'ChooseCredits', 3, ['CIT100', 'CIT101']),
            ('GEN-AR', 'Communication Skills in Arabic (choose one)', 'ChooseCredits', 3, ['GEN100', 'GEN101']),
            ('GEN', 'General requirements', 'AllOf', None, ['ENG100', 'GEN102', 'GEN105', 'ENG104', 'GEN103', 'GEN201', 'GEN302', 'GEN304', 'MTH105']),
            ('CORE', 'Core requirements', 'AllOf', None, ['BUS100', 'ACC106', 'MKT200', 'ACC200', 'HRM201', 'BUS201', 'BUS203', 'BIT203', 'MTH203',
                                                          'BUS301', 'BAF301', 'BUS306', 'BUS307', 'BUS309', 'BUS409', 'BUS417']),
            ('MAJOR', 'Major requirements', 'AllOf', None, ['BIT201', 'BIT205', 'BIT207', 'BUS302', 'BIT305', 'BIT405', 'BIT407', 'BIT413', 'BIT415',
                                                            'BIT430', 'BIT417', 'BIT422', 'BIT419']),
            ('MAJ-ELEC', 'Major elective (choose one)', 'ChooseCredits', 3, ['BUS421', 'BAF403', 'MKT301', 'BUS411']),
        ],
        semesters={
            1: ['ENG100', 'GEN-AR', 'GEN102', 'GEN-IT', 'BUS100'],
            2: ['BIT201', 'MTH105', 'GEN105', 'BUS201', 'ACC106'],
            3: ['BIT203', 'BIT413', 'BAF301', 'BIT205', 'BIT305'],
            4: ['BIT207', 'BUS203', 'BUS301', 'MKT200', 'GEN302'],
            5: ['BUS302', 'BIT407', 'GEN304', 'BIT417', 'MAJ-ELEC', 'MTH203'],
            6: ['ENG104', 'GEN103', 'HRM201', 'ACC200', 'BUS306'],
            7: ['GEN201', 'BUS307', 'BUS309', 'BIT405', 'BUS417'],
            8: ['BIT415', 'BIT430', 'BIT422', 'BUS409', 'BIT419'],
        }),
    'BIT': dict(
        name='Bachelor of Information Technology (BIT) — General Track', ar='بكالوريوس تقنية المعلومات - المسار العام',
        college='College of Information Technology', pages='194-197',
        groups=[
            ('GEN-IT', 'Introduction to IT or Generative AI (choose one)', 'ChooseCredits', 3, ['CIT100', 'CIT101']),
            ('GEN-AR', 'Communication Skills in Arabic (choose one)', 'ChooseCredits', 3, ['GEN100', 'GEN101']),
            ('GEN', 'General requirements', 'AllOf', None, ['ENG100', 'GEN102', 'GEN105', 'ENG104', 'GEN103', 'GEN201', 'GEN302', 'GEN304', 'MTH105']),
            ('CORE', 'Core requirements', 'AllOf', None, ['CIT112', 'CIT121', 'CIT122', 'CIT123', 'CIT231', 'CIT232', 'CIT241', 'CIT242', 'CIT244',
                                                          'BIT203', 'CIT234', 'CIT353', 'CIT354', 'BUS407', 'CIT362', 'BIM200', 'CIT483', 'CIT472',
                                                          'MTH110', 'CIT478', 'CIT473', 'CIT484', 'CIT485']),
            ('TRACK', 'General track courses', 'AllOf', None, ['CIT233', 'CIT243', 'CIT352', 'CIT365', 'CIT481']),
            ('MAJ-ELEC', 'Electives (choose two)', 'ChooseCredits', 6, ['CIT474', 'CIT487', 'CIT477', 'CIT470']),
        ],
        semesters={
            1: ['ENG100', 'GEN-IT', 'GEN-AR', 'CIT112', 'CIT121'],
            2: ['BIT203', 'ENG104', 'MTH105', 'CIT123', 'CIT122'],
            3: ['GEN102', 'CIT231', 'CIT232', 'CIT353', 'CIT244', 'GEN105'],  # the plan says CIT246; the core table says CIT244
            4: ['CIT241', 'CIT242', 'CIT354', 'GEN302', 'CIT234', 'GEN201'],
            5: ['GEN103', 'MTH110', 'CIT362', 'BUS407', 'CIT472'],
            6: ['GEN304', 'BIM200', 'CIT473', 'MAJ-ELEC', 'CIT483'],
            7: ['MAJ-ELEC', 'CIT484', 'CIT233', 'CIT243', 'CIT352'],  # "General Track #1-#3"
            8: ['CIT478', 'CIT485', 'CIT365', 'CIT481'],  # "General Track #4-#5"
        }),
    'BMC': dict(
        name='Bachelor of Mass Communication Concentration in Public Relations', ar='بكالوريوس الاتصال الجماهيري - تخصص العلاقات العامة',
        college='College of Media and Public Relations', pages='284-287',
        groups=[
            ('GEN-IT', 'Introduction to IT or Generative AI (choose one)', 'ChooseCredits', 3, ['CIT100', 'CIT101']),
            ('GEN-AR', 'Communication Skills in Arabic (choose one)', 'ChooseCredits', 3, ['GEN100', 'GEN101']),
            ('GEN', 'General requirements', 'AllOf', None, ['ENG100', 'GEN102', 'GEN105', 'ENG104', 'GEN103', 'GEN201', 'GEN302', 'GEN304', 'MTH105']),
            ('PROG', 'Program requirements', 'AllOf', None, ['MCM101', 'MCM102', 'MCM121', 'MCM123', 'MCM201', 'MCM202', 'MCM203', 'MCM301']),
            ('PROG-ELEC', 'Program elective (choose one)', 'ChooseCredits', 3, ['MCM210', 'MCM211', 'MCM212', 'MCM213']),
            ('SUPP', 'Supporting requirements', 'AllOf', None, ['MCM122', 'MCM204', 'MCM221', 'MCM222', 'MCM223', 'MCM321', 'MCM322', 'MCM401', 'MCM421']),
            ('SUPP-ELEC', 'Supporting elective (choose one)', 'ChooseCredits', 3, ['MCM125', 'MCM326', 'MCM327', 'MCM328', 'MCM329']),
            ('MAJOR', 'Public relations major requirements', 'AllOf', None, ['PRM301', 'PRM302', 'PRM303', 'PRM321', 'PRM401', 'PRM402', 'PRM403', 'PRM410', 'PRM422']),
            ('FREE', 'Free courses (two, any course)', 'AnyCourse', 6, []),
        ],
        semesters={
            1: ['ENG100', 'GEN105', 'GEN102', 'GEN-IT', 'MCM101', 'MCM102'],
            2: ['MCM121', 'ENG104', 'MCM122', 'MCM123', 'MTH105'],
            3: ['GEN-AR', 'MCM201', 'MCM202', 'MCM203', 'MCM204', 'PROG-ELEC'],
            4: ['MCM221', 'MCM222', 'MCM223', 'PRM301', 'GEN304', 'FREE'],  # the plan prints "GEN 301 Professional Ethics"
            5: ['GEN302', 'MCM301', 'PRM302', 'PRM303'],  # the plan lists ENG104 here again (duplicate of Year 1)
            6: ['GEN201', 'PRM321', 'MCM321', 'MCM322', 'SUPP-ELEC'],
            7: ['GEN103', 'PRM401', 'MCM401', 'PRM402', 'PRM410'],
            8: ['MCM421', 'FREE', 'PRM403', 'PRM422'],
        }),
}

for key, p in PROGRAMS.items():
    total = 0
    in_groups = set()
    for gkey, _, gtype, gcred, courses in p['groups']:
        total += gcred if gtype != 'AllOf' else sum(C[x]['credits'] for x in courses)
        in_groups |= set(courses)
        for x in courses:
            assert x in C, (key, x)
    assert total == 123, (key, total)
    slots = [s for sem in p['semesters'].values() for s in sem]
    slot_credits = sum(3 for s in slots)
    assert slot_credits == 123, (key, slot_credits)
    group_keys = {g[0] for g in p['groups']}
    for s in slots:
        assert s in C or s in group_keys, (key, s)
    for gkey, _, gtype, _, courses in p['groups']:
        if gtype == 'AllOf':
            for x in courses:
                assert x in slots, (key, 'not in the study plan', x)
    print(f'{key}: 123 credits, {len(in_groups)} courses in its plan', file=sys.stderr)


def suggested(prog, code_or_group):
    for n, sem in PROGRAMS[prog]['semesters'].items():
        if code_or_group in sem:
            return n
    return ''


def cycle_check():
    graph = {k: re.findall(r'[A-Z]{2,4}\d{3}', v['pre']) for k, v in C.items()}
    state = {}

    def visit(n, path):
        if state.get(n) == 1:
            raise AssertionError('cycle: ' + ' -> '.join(path + [n]))
        if state.get(n) == 2:
            return
        state[n] = 1
        for m in graph[n]:
            visit(m, path + [n])
        state[n] = 2

    for n in graph:
        visit(n, [])


cycle_check()


def write(name, header, rows):
    with open(os.path.join(OUT, name), 'w', encoding='utf-8', newline='') as f:
        w = csv.writer(f, lineterminator='\n')
        w.writerow(header)
        w.writerows(rows)


def course_source(code):
    d = DESCRIPTIONS.get(code)
    return f"{CATALOG} p.{C[code]['page']}" + (f"; description p.{d['pdf_page']}" if d else '')


write('courses.csv', ['course_code', 'title', 'title_ar', 'credits', 'department', 'prerequisites', 'offered', 'description', 'source'],
      [[k, x['title'], x['ar'], x['credits'], x['dept'], x['pre'], EVERY_TERM, (DESCRIPTIONS.get(k) or {}).get('description', ''), course_source(k)]
       for k, x in C.items()])
write('programs.csv', ['program_code', 'name', 'name_ar', 'college', 'total_credits'],
      [[k, p['name'], p['ar'], p['college'], 123] for k, p in PROGRAMS.items()])

dp = []
for key, p in PROGRAMS.items():
    src = f"{CATALOG} p.{p['pages']}"
    for gkey, gname, gtype, gcred, courses in p['groups']:
        if gtype == 'AnyCourse':
            dp.append([key, 2025, gkey, gname, gtype, gcred, '', suggested(key, gkey), src])
            continue
        for x in courses:
            sem = suggested(key, x) or (suggested(key, gkey) if gtype != 'AllOf' else '')
            dp.append([key, 2025, gkey, gname, gtype, gcred if gcred else '', x, sem, src])
write('degree-plans.csv', ['program_code', 'catalog_year', 'group_key', 'group_name', 'group_type', 'group_credits', 'course_code', 'suggested_semester', 'source'], dp)

# Liwa names terms by academic year ("Fall 2026-2027", "Spring 2026-2027", "Summer 1 2025-2026"). Codes stay
# calendar-year (2027SP = Spring 2026-2027). A Summer 2 is assumed to exist but isn't modelled yet (assumptions.md).
write('terms.csv', ['term_code', 'name', 'season', 'year', 'start_date', 'end_date', 'teaching_days', 'friday_afternoon', 'is_current'], [
    ['2026SP', 'Spring 2025-2026', 'Spring', 2026, '2026-01-12', '2026-05-08', 'Mon;Tue;Wed;Thu;Fri', 'no', 'no'],
    ['2026SU', 'Summer 1 2025-2026', 'Summer', 2026, '2026-06-08', '2026-07-24', 'Mon;Tue;Wed;Thu', 'no', 'no'],
    ['2026FA', 'Fall 2026-2027', 'Fall', 2026, '2026-08-24', '2026-12-18', 'Mon;Tue;Wed;Thu;Fri', 'no', 'no'],
    ['2027SP', 'Spring 2026-2027', 'Spring', 2027, '2027-01-11', '2027-05-07', 'Mon;Tue;Wed;Thu;Fri', 'no', 'yes'],
    ['2027SU', 'Summer 1 2026-2027', 'Summer', 2027, '2027-06-07', '2027-07-30', 'Mon;Tue;Wed;Thu', 'no', 'no'],
    ['2027FA', 'Fall 2027-2028', 'Fall', 2027, '2027-08-23', '2027-12-17', 'Mon;Tue;Wed;Thu;Fri', 'no', 'no'],
])

# ---- Spring 2027 timetable (illustrative; UAE patterns: MW, TR, MWF mornings, Mon–Thu evenings; no Sunday; nothing
# after 12:00 on Friday). Every course is 3 credits.
day_slots = [('Mon;Wed', '09:00', '10:15'), ('Tue;Thu', '09:00', '10:15'), ('Mon;Wed', '11:00', '12:15'),
             ('Tue;Thu', '11:00', '12:15'), ('Mon;Wed', '14:00', '15:15'), ('Tue;Thu', '14:00', '15:15')]
mwf = [('Mon;Wed;Fri', '08:00', '08:50'), ('Mon;Wed;Fri', '09:00', '09:50'), ('Mon;Wed;Fri', '10:00', '10:50'), ('Mon;Wed;Fri', '11:00', '11:50')]
eve = [('Mon;Wed', '17:00', '18:15'), ('Tue;Thu', '17:00', '18:15'), ('Mon;Wed', '18:30', '19:45'), ('Tue;Thu', '18:30', '19:45')]
instructors = ['Dr. Layla Haddad', 'Dr. Yousef Karim', 'Dr. Hana Saleh', 'Dr. Omar Farouk', 'Dr. Reem Nasser',
               'Dr. Tariq Aziz', 'Dr. Salma Idris', 'Dr. Faisal Rahman', 'Dr. Nadia Khoury', 'Dr. Khalid Mansour']
NO_SPRING_SECTIONS = {'MCM329'}  # offered every term in the catalog but nothing scheduled: the NotScheduled warning case
POPULAR = {'ENG100', 'ENG104', 'MTH105', 'GEN102', 'GEN105', 'BUS100', 'CIT112', 'CIT101', 'MCM101', 'BIT203'}
sec = []
for i, code in enumerate([k for k in C if k not in NO_SPRING_SECTIONS], start=1):
    x = C[code]
    ins = instructors[i % 10]
    cap = 30
    level = int(re.search(r'\d{3}', code).group()) // 100

    def seats(k):
        return (i * 7 + k * 5) % cap

    def row(section, slot, room, teacher=ins, k=0, delivery='OnCampus', capacity=cap):
        days, start, end = slot
        sec.append(['2027SP', code, section, delivery, teacher, room, capacity, seats(k) % capacity, days, start, end])

    row('01', day_slots[i % 6], f'A{100 + i}')
    if x['dept'] == GE or code in POPULAR:
        row('02', mwf[i % 4], f'A{200 + i}', instructors[(i + 3) % 10], k=1)
    # Evening sections: all general education and first/second-year courses, and every other course beyond that,
    # so working students (Mon–Thu 5–8 pm) can still make progress.
    if x['dept'] == GE or level <= 2 or i % 2 == 0:
        row('E1', eve[i % 4], f'A{300 + i}', instructors[(i + 5) % 10], k=2)
    if i % 5 == 0:
        row('W1', ('', '', ''), '', instructors[(i + 7) % 10], k=3, delivery='Online', capacity=40)
    if x['dept'] != GE and i % 3 == 1 and code not in POPULAR:
        row('02', day_slots[(i + 2) % 6], f'A{400 + i}', instructors[(i + 4) % 10], k=4)

# Some sections are full, so the planner has to pick another (never the only section of a course).
by_course = {}
for r in sec:
    by_course.setdefault(r[1], []).append(r)
full = 0
for n, (code, rows) in enumerate(sorted(by_course.items())):
    if len(rows) >= 2 and n % 6 == 3 and code != 'MTH105':
        rows[0][7] = rows[0][6]
        full += 1
# Seats decide between two class times: MTH105 section 02 has more seats left than 01 (brief §8 decision log test).
for r in by_course['MTH105']:
    if r[2] == '01':
        r[7] = r[6] - 5
    if r[2] == '02':
        r[7] = 5
assert {r[2] for r in by_course['MTH105']} >= {'01', '02'}
for r in sec:
    if r[8]:
        assert 'Sun' not in r[8], r
        if 'Fri' in r[8]:
            assert r[10] <= '12:00', r
write('sections.csv', ['term_code', 'course_code', 'section', 'delivery', 'instructor', 'room', 'capacity', 'enrolled', 'days', 'start_time', 'end_time'], sec)
print('sections', len(sec), 'full', full, file=sys.stderr)

# ---- 16 demo students (brief §5 stories on the real plans). The hold column is the PeopleSoft service indicator.
students = [
    ['202700101', 'Mariam Al Mansoori', '202700101@lu.ac.ae', 'student.new', 'BIT', 2025, '2027SP', 'Good', 'advisor.it', ''],
    ['202500102', 'Khalid Al Hammadi', '202500102@lu.ac.ae', 'student.probation', 'BBA-BIT', 2025, '2025FA', 'Probation', 'advisor.business', ''],
    ['202500103', 'Aisha Al Nuaimi', '202500103@lu.ac.ae', 'student.honors', 'BIT', 2025, '2025FA', 'Good', 'advisor.it', ''],
    ['202300104', 'Omar Al Shamsi', '202300104@lu.ac.ae', 'student.senior', 'BIT', 2025, '2023FA', 'Good', 'advisor.it', ''],
    ['202500105', 'Noura Al Ketbi', '202500105@lu.ac.ae', 'student.repeat', 'BMC', 2025, '2025FA', 'Good', 'advisor.media', ''],
    ['202600106', 'Saeed Al Dhaheri', '202600106@lu.ac.ae', 'student.first', 'BIT', 2025, '2026FA', 'Good', 'advisor.it', ''],
    ['202600107', 'Fatima Al Kaabi', '202600107@lu.ac.ae', 'student.switch', 'BBA-BIT', 2025, '2025FA', 'Good', 'advisor.business', ''],
    ['202500108', 'Hamad Al Marri', '202500108@lu.ac.ae', 'student.mid1', 'BBA-BIT', 2025, '2025FA', 'Good', 'advisor.business', ''],
    ['202400109', 'Latifa Al Suwaidi', '202400109@lu.ac.ae', 'student.media', 'BMC', 2025, '2024FA', 'Good', 'advisor.media', ''],
    ['202400110', 'Rashid Al Falasi', '202400110@lu.ac.ae', 'student.mid2', 'BIT', 2025, '2024FA', 'Good', 'advisor.it', ''],
    ['202600111', 'Yousef Al Hosani', '202600111@lu.ac.ae', 'student.evening', 'BIT', 2025, '2026FA', 'Good', 'advisor.it', ''],
    ['202500112', 'Maryam Al Blooshi', '202500112@lu.ac.ae', 'student.transfer', 'BBA-BIT', 2025, '2025FA', 'Good', 'advisor.business', ''],
    ['202500113', 'Sultan Al Mehairi', '202500113@lu.ac.ae', 'student.hold', 'BMC', 2025, '2025FA', 'Good', 'advisor.media', 'FINANCE'],
    ['202600114', 'Hessa Al Ali', '202600114@lu.ac.ae', 'student.parttime', 'BBA-BIT', 2025, '2026FA', 'Good', 'advisor.business', ''],
    ['202300115', 'Mohammed Al Junaibi', '202300115@lu.ac.ae', 'student.intern', 'BIT', 2025, '2023FA', 'Good', 'advisor.it', ''],
    # A real BBA-BIT transcript supplied by the project owner; name, ID and email replaced.
    ['202600116', 'Demo Student H', '', 'student.hanieh', 'BBA-BIT', 2025, '2026SP', 'Good', 'advisor.business', ''],
]
assert len(students) == 16 and all(s[3] for s in students)
write('students.csv', ['student_id', 'display_name', 'email', 'username', 'program_code', 'catalog_year', 'admit_term', 'status', 'advisor_username', 'hold'], students)

PTS = {'A': 4, 'A-': 3.7, 'B+': 3.3, 'B': 3, 'B-': 2.7, 'C+': 2.3, 'C': 2, 'C-': 1.7, 'D+': 1.3, 'D': 1, 'F': 0}
tr = []


def add(sid, term, code, grade=None):
    assert code in C, code
    tr.append([sid, term, code, '', grade or '', 'Completed' if grade else 'InProgress', ''])


def transfer(sid, code, source):
    tr.append([sid, '', code, '', '', 'Transfer', source])


# Choice slots resolved for transcripts (students who joined before Fall 2025 took CIT100; later ones CIT101).
CHOICE = {'GEN-AR': 'GEN100', 'MAJ-ELEC': None, 'PROG-ELEC': 'MCM210', 'SUPP-ELEC': 'MCM327', 'FREE': None}
ELECTIVES = {'BBA-BIT': ['BUS411'], 'BIT': ['CIT474', 'CIT470'], 'BMC': []}
FREE_PICKS = ['BUS100', 'MKT200']  # free courses: outside the BMC plan, so they can only count as free


def semester_courses(prog, n, joined_before_fall_2025):
    out = []
    electives = list(ELECTIVES[prog])
    free = list(FREE_PICKS)
    for m in range(1, n + 1):
        for s in PROGRAMS[prog]['semesters'][m]:
            if s == 'GEN-IT':
                out.append((m, 'CIT100' if joined_before_fall_2025 else 'CIT101'))
            elif s == 'MAJ-ELEC':
                out.append((m, electives.pop(0) if m <= n else None))
            elif s == 'FREE':
                out.append((m, free.pop(0)))
            elif s in CHOICE:
                out.append((m, CHOICE[s]))
            else:
                out.append((m, s))
    return [(m, x) for m, x in out if x]


def history(sid, prog, terms, grades, joined_before_fall_2025, skip=(), in_progress_last=False):
    """Every course of semesters 1..len(terms), semester k taken in terms[k-1]. Grades cycle through `grades`."""
    n = 0
    for m, code in semester_courses(prog, len(terms), joined_before_fall_2025):
        if code in skip:
            continue
        term = terms[m - 1]
        if in_progress_last and m == len(terms):
            add(sid, term, code)
        else:
            add(sid, term, code, grades[n % len(grades)])
            n += 1


def summary(sid):
    order = lambda t: int(t[:4]) * 10 + {'SP': 1, 'SU': 2, 'FA': 3}[t[4:]]
    last = {}
    for r in sorted([r for r in tr if r[0] == sid and r[5] == 'Completed'], key=lambda r: order(r[1])):
        last[r[2]] = r
    num = sum(PTS[r[4]] * C[r[2]]['credits'] for r in last.values())
    den = sum(C[r[2]]['credits'] for r in last.values())
    earned = sum(C[r[2]]['credits'] for r in last.values() if r[4] != 'F')
    earned += sum(C[r[2]]['credits'] for r in tr if r[0] == sid and r[5] == 'Transfer')
    return (round(num / den + 1e-9, 2) if den else None), earned


FALL23 = ['2023FA', '2024SP', '2024FA', '2025SP', '2025FA', '2026SP', '2026FA']
FALL24 = ['2024FA', '2025SP', '2025FA', '2026SP', '2026FA']
FALL25 = ['2025FA', '2026SP', '2026FA']
GOOD = ['A', 'B+', 'A-', 'B', 'B+', 'A-', 'B', 'A', 'C+', 'B+']

# student.new (BIT): first semester starts now; only an AP credit for ENG100.
transfer('202700101', 'ENG100', 'Advanced Placement (AP English)')
# student.probation (BBA-BIT): GPA below 2.0; failed BUS201 and repeated it.
history('202500102', 'BBA-BIT', FALL25, ['C-', 'D', 'C', 'D+', 'C', 'D', 'F', 'D+', 'C', 'C-', 'D', 'C', 'D', 'C-', 'D'], False, skip={'BUS201'})
add('202500102', '2026SP', 'BUS201', 'F')
add('202500102', '2026FA', 'BUS201', 'C-')
# student.honors (BIT): exactly 3.50 on completed work (the 21-credit boundary); Fall 2026 grades not posted yet.
history('202500103', 'BIT', FALL25, ['A', 'B'], False, in_progress_last=True)
# student.senior (BIT): everything except CIT485 and the second elective (CIT470): 6 credits left.
history('202300104', 'BIT', FALL23 + ['2027SP'], GOOD, True, skip={'CIT485', 'CIT470'})
tr[:] = [r for r in tr if not (r[0] == '202300104' and r[1] == '2027SP')]
for code, term in [('CIT478', '2026FA'), ('CIT365', '2026FA'), ('CIT481', '2026FA')]:
    add('202300104', term, code, 'B+')
# student.repeat (BMC): failed MCM123 Media Theories, which MCM202 Media Research Methods needs.
history('202500105', 'BMC', FALL25, ['B', 'B+', 'A-', 'A', 'B', 'B-', 'C+', 'B', 'B', 'A'], False, skip={'MCM123', 'MCM202'})
add('202500105', '2026SP', 'MCM123', 'F')
# student.first (BIT): first semester done.
history('202600106', 'BIT', ['2026FA'], ['B', 'B+', 'A-', 'A', 'B'], False)
# student.switch (BBA-BIT): started in Mass Communication (Fall 2025), switched to Business in Fall 2026. MCM101 and
# MCM123 stay on the transcript but don't count toward the BBA.
for code, g in [('MCM101', 'B'), ('MCM123', 'B+'), ('GEN100', 'A-')]:
    add('202600107', '2025FA', code, g)
for code, g in [('ENG100', 'A'), ('BUS100', 'A-'), ('GEN102', 'B+'), ('CIT101', 'A')]:
    add('202600107', '2026FA', code, g)
# student.mid1 (BBA-BIT): three semesters done.
history('202500108', 'BBA-BIT', FALL25, ['B', 'B', 'C+', 'B', 'B-', 'C', 'B', 'C+', 'B-'], False)
# student.media (BMC) and student.mid2 (BIT): five semesters done.
history('202400109', 'BMC', FALL24, ['A', 'A', 'A-', 'A-', 'A', 'B+', 'B', 'A-', 'B+', 'A'], True)
history('202400110', 'BIT', FALL24, ['C+', 'B-', 'C', 'B', 'C+', 'C-', 'B', 'C', 'B-'], True)
# student.evening (BIT): first semester done; can only study Mon–Thu 5–8 pm (saved as free time by the seeder).
history('202600111', 'BIT', ['2026FA'], ['B', 'C+', 'B', 'B+', 'B-'], False)
# student.transfer (BBA-BIT): three courses accepted from another university, then three semesters here.
for code in ('ENG100', 'MTH105', 'GEN102'):
    transfer('202500112', code, 'Higher Colleges of Technology')
history('202500112', 'BBA-BIT', FALL25, ['B+', 'B', 'A-', 'A', 'B', 'B-', 'C+', 'A'], False, skip={'ENG100', 'MTH105', 'GEN102'})
# student.hold (BMC): a FINANCE hold; good record otherwise.
history('202500113', 'BMC', FALL25, ['B+', 'A-', 'B', 'A', 'B', 'B+', 'C+', 'A-'], False)
# student.parttime (BBA-BIT): two courses a semester while working.
for code, g in [('ENG100', 'B'), ('BUS100', 'B+')]:
    add('202600114', '2026FA', code, g)
# student.intern (BIT): well past the internship's 75 credits; internship and graduation projects still to come.
history('202300115', 'BIT', FALL23, GOOD, True, skip={'CIT483', 'CIT484', 'CIT470'})
# student.hanieh (BBA-BIT): the real record (Spring 2025-2026 = 2026SP, Summer 1 2025-2026 = 2026SU, Fall 2026-2027 = 2026FA).
for term, code, grade in [('2026SP', 'ENG100', 'A'), ('2026SP', 'GEN100', 'A'), ('2026SP', 'BUS100', 'A'), ('2026SP', 'BUS201', 'A'),
                          ('2026SU', 'CIT100', 'A'), ('2026SU', 'GEN105', 'A'),
                          ('2026FA', 'ACC106', None), ('2026FA', 'BIT203', None), ('2026FA', 'BUS301', None), ('2026FA', 'ENG104', None)]:
    add('202600116', term, code, grade)
write('transcripts.csv', ['student_id', 'term_code', 'course_code', 'credits', 'grade', 'status', 'source'], tr)

g, e = summary('202500103'); print('honors', g, e, file=sys.stderr); assert g == 3.50
g, e = summary('202500102'); print('probation', g, e, file=sys.stderr); assert g < 2.0
g, e = summary('202300104'); print('senior', g, e, file=sys.stderr); assert e == 117
g, e = summary('202700101'); print('new', g, e, file=sys.stderr); assert e == 3
g, e = summary('202300115'); print('intern', g, e, file=sys.stderr); assert e >= 75
g, e = summary('202600116'); print('hanieh', g, e, file=sys.stderr); assert (g, e) == (4.0, 18)
assert any(r[0] == '202500105' and r[2] == 'MCM123' and r[4] == 'F' for r in tr)
assert sum(1 for r in tr if r[0] == '202500112' and r[5] == 'Transfer') == 3
for r in tr:
    assert r[2] in C, r
print(f'courses {len(C)}, prerequisite differences {len(DIFFS)}', file=sys.stderr)
print('all checks passed', file=sys.stderr)
