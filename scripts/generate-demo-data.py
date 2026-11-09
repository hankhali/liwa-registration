"""Generates the illustrative demo data (CSV, import-contract format) and checks it.
Usage: python -I gen_demo.py <output dir>"""
import csv, os, re, sys

OUT = sys.argv[1]
ALL, FS, F, SP, SU = 'Fall;Spring;Summer', 'Fall;Spring', 'Fall', 'Spring', 'Summer'
C = []  # code, title, title_ar, credits, dept, prerequisites, offered


def c(code, title, ar, cr, dept, pre='', off=FS):
    C.append((code, title, ar, cr, dept, pre, off))


GE = 'General Education'
c('ENG101', 'English Composition I', 'الكتابة الإنجليزية 1', 3, GE, '', ALL)
c('ENG102', 'English Composition II', 'الكتابة الإنجليزية 2', 3, GE, 'ENG101', ALL)
c('ENG201', 'Technical and Professional Writing', 'الكتابة التقنية والمهنية', 3, GE, 'ENG102')
c('ARB101', 'Arabic Language Skills', 'مهارات اللغة العربية', 3, GE, '', ALL)
c('ISL101', 'Islamic Culture', 'الثقافة الإسلامية', 3, GE, '', ALL)
c('GEN100', 'Student Success Seminar', 'ندوة النجاح الجامعي', 1, GE, '', FS)
c('GEN101', 'UAE Society', 'المجتمع الإماراتي', 3, GE, '', ALL)
c('GEN110', 'Innovation and Entrepreneurship', 'الابتكار وريادة الأعمال', 3, GE, '', FS)
c('GEN120', 'Critical Thinking', 'التفكير الناقد', 3, GE, 'ENG101', FS)
c('PED101', 'Health and Wellness', 'الصحة واللياقة', 2, GE, '', ALL)
c('MTH101', 'College Mathematics', 'الرياضيات الجامعية', 3, GE, '', ALL)
c('MTH110', 'Calculus I', 'التفاضل والتكامل 1', 4, GE, 'MTH101:C', FS)
c('STA201', 'Introduction to Statistics', 'مقدمة في الإحصاء', 3, GE, 'MTH101', ALL)
IT = 'Information Technology'
c('CSC101', 'Introduction to Programming', 'مقدمة في البرمجة', 3, IT, 'COREQ(CSC101L)')
c('CSC101L', 'Programming Lab', 'مختبر البرمجة', 1, IT, 'COREQ(CSC101)')
c('CSC102', 'Object-Oriented Programming', 'البرمجة كائنية التوجه', 3, IT, 'CSC101')
c('CSC201', 'Data Structures', 'هياكل البيانات', 3, IT, 'CSC102')
c('CSC210', 'Discrete Mathematics', 'الرياضيات المتقطعة', 3, IT, 'MTH101')
c('ITC101', 'Introduction to Information Technology', 'مقدمة في تقنية المعلومات', 3, IT, '', ALL)
c('ITC110', 'Computer Hardware Fundamentals', 'أساسيات عتاد الحاسوب', 3, IT, 'COREQ(ITC110L)')
c('ITC110L', 'Computer Hardware Lab', 'مختبر عتاد الحاسوب', 1, IT, 'COREQ(ITC110)')
c('ITC201', 'Database Systems', 'نظم قواعد البيانات', 3, IT, 'CSC102')
c('ITC202', 'Computer Networks', 'شبكات الحاسوب', 3, IT, 'ITC110 AND COREQ(ITC202L)')
c('ITC202L', 'Computer Networks Lab', 'مختبر شبكات الحاسوب', 1, IT, 'COREQ(ITC202)')
c('ITC210', 'Web Development', 'تطوير الويب', 3, IT, 'CSC101')
c('ITC220', 'Operating Systems', 'نظم التشغيل', 3, IT, 'CSC201', F)
c('ITC250', 'Software Engineering', 'هندسة البرمجيات', 3, IT, 'CSC201')
c('ITC301', 'Systems Analysis and Design', 'تحليل النظم وتصميمها', 3, IT, 'ITC201')
c('ITC304', 'Information Security', 'أمن المعلومات', 3, IT, 'ITC202', SP)
c('ITC310', 'Cloud Computing', 'الحوسبة السحابية', 3, IT, 'ITC202 AND ITC220')
c('ITC320', 'Mobile App Development', 'تطوير تطبيقات الهاتف', 3, IT, 'ITC210 AND CSC102')
c('ITC330', 'Human-Computer Interaction', 'التفاعل بين الإنسان والحاسوب', 3, IT, 'ITC210')
c('ITC340', 'IT Project Management', 'إدارة مشاريع تقنية المعلومات', 3, IT, 'ITC301 AND STANDING(Junior)')
c('ITC350', 'Data Analytics', 'تحليل البيانات', 3, IT, 'ITC201 AND STA201')
c('ITC360', 'Artificial Intelligence Fundamentals', 'أساسيات الذكاء الاصطناعي', 3, IT, 'CSC201 AND STA201')
c('ITC370', 'Network Administration', 'إدارة الشبكات', 3, IT, 'ITC202')
c('ITC380', 'Digital Forensics', 'التحقيق الجنائي الرقمي', 3, IT, 'ITC304')
c('ITC390', 'Special Topics in IT', 'موضوعات خاصة في تقنية المعلومات', 3, IT, 'STANDING(Junior)')
c('ITC401', 'Capstone Project I', 'مشروع التخرج 1', 3, IT, 'ITC301 AND STANDING(Senior)', F)
c('ITC402', 'Capstone Project II', 'مشروع التخرج 2', 4, IT, 'ITC401', SP)
c('ITC410', 'IT Internship', 'التدريب العملي في تقنية المعلومات', 4, IT, 'CREDITS(90) AND PERMISSION', SU)
BUS = 'Business'
c('BUS101', 'Introduction to Business', 'مقدمة في إدارة الأعمال', 3, BUS, '', ALL)
c('BUS110', 'Business Communication', 'الاتصال في الأعمال', 3, BUS, 'ENG101')
c('BUS120', 'Microeconomics', 'الاقتصاد الجزئي', 3, BUS, 'MTH101')
c('BUS121', 'Macroeconomics', 'الاقتصاد الكلي', 3, BUS, 'BUS120')
c('BUS205', 'Spreadsheet Modelling Lab', 'مختبر نمذجة الجداول الإلكترونية', 2, BUS, 'BUS101')
c('BUS210', 'Business Law', 'قانون الأعمال', 3, BUS, 'BUS101')
c('BUS220', 'Business Statistics', 'إحصاء الأعمال', 3, BUS, 'STA201')
c('BUS230', 'Management Information Systems', 'نظم المعلومات الإدارية', 3, BUS, 'BUS101')
c('BUS301', 'Business Ethics', 'أخلاقيات الأعمال', 3, BUS, 'STANDING(Junior)')
c('BUS310', 'Business Research Methods', 'مناهج البحث في الأعمال', 3, BUS, 'BUS220')
c('BUS401', 'Strategic Management', 'الإدارة الاستراتيجية', 4, BUS, 'MGT201 AND FIN201 AND STANDING(Senior)', SP)
c('BUS410', 'Business Internship', 'التدريب العملي في الأعمال', 4, BUS, 'CREDITS(90) AND PERMISSION', SU)
c('ACC101', 'Financial Accounting', 'المحاسبة المالية', 3, BUS, 'MTH101')
c('ACC102', 'Managerial Accounting', 'المحاسبة الإدارية', 3, BUS, 'ACC101')
c('FIN201', 'Principles of Finance', 'مبادئ التمويل', 3, BUS, 'ACC101 AND BUS120')
c('FIN301', 'Corporate Finance', 'تمويل الشركات', 3, BUS, 'FIN201', F)
c('MGT201', 'Principles of Management', 'مبادئ الإدارة', 3, BUS, 'BUS101')
c('MGT210', 'Organizational Behavior', 'السلوك التنظيمي', 3, BUS, 'MGT201')
c('MGT220', 'Human Resource Management', 'إدارة الموارد البشرية', 3, BUS, 'MGT201')
c('MGT230', 'Operations Management', 'إدارة العمليات', 3, BUS, 'MGT201 AND BUS220')
c('MGT290', 'Professional Skills Workshop', 'ورشة المهارات المهنية', 2, BUS, 'MGT201')
c('MGT301', 'Leadership', 'القيادة', 3, BUS, 'MGT210')
c('MGT310', 'Project Management', 'إدارة المشاريع', 3, BUS, 'MGT201')
c('MGT320', 'Entrepreneurship and Small Business', 'ريادة الأعمال والمشروعات الصغيرة', 3, BUS, 'MGT201')
c('MGT330', 'International Business', 'الأعمال الدولية', 3, BUS, 'BUS121')
c('MGT340', 'Change Management', 'إدارة التغيير', 3, BUS, 'MGT210')
c('MGT350', 'Quality Management', 'إدارة الجودة', 3, BUS, 'MGT230')
c('MGT360', 'Negotiation', 'التفاوض', 3, BUS, 'MGT201')
c('MGT370', 'Strategic Human Resource Management', 'الإدارة الاستراتيجية للموارد البشرية', 3, BUS, 'MGT220')
c('MGT380', 'Business Analytics', 'تحليلات الأعمال', 3, BUS, 'BUS220 AND BUS230')
MC = 'Mass Communication'
c('MCM101', 'Introduction to Mass Communication', 'مقدمة في الاتصال الجماهيري', 3, MC, '', ALL)
c('MCM110', 'Media Writing', 'الكتابة الإعلامية', 3, MC, 'ENG101')
c('MCM120', 'Visual Communication', 'الاتصال البصري', 3, MC, '')
c('MCM130', 'Photography', 'التصوير الفوتوغرافي', 3, MC, 'COREQ(MCM130L)')
c('MCM130L', 'Photography Lab', 'مختبر التصوير', 1, MC, 'COREQ(MCM130)')
c('MCM201', 'Principles of Journalism', 'مبادئ الصحافة', 3, MC, 'MCM110', F)
c('MCM210', 'Principles of Public Relations', 'مبادئ العلاقات العامة', 3, MC, 'MCM101')
c('MCM220', 'Media Law and Ethics', 'قوانين الإعلام وأخلاقياته', 3, MC, 'MCM101')
c('MCM230', 'Broadcast Production', 'الإنتاج الإذاعي والتلفزيوني', 3, MC, 'MCM120')
c('MCM240', 'Digital Media', 'الإعلام الرقمي', 3, MC, 'MCM120')
c('MCM250', 'Arabic Media Writing', 'الكتابة الإعلامية باللغة العربية', 3, MC, 'ARB101 AND MCM110')
c('MCM290', 'Portfolio Workshop', 'ورشة الملف المهني', 3, MC, 'MCM110')
c('MCM301', 'News Reporting and Editing', 'كتابة الأخبار وتحريرها', 3, MC, 'MCM201:C')
c('MCM310', 'Advertising', 'الإعلان', 3, MC, 'MCM210')
c('MCM320', 'Media Research Methods', 'مناهج البحث الإعلامي', 3, MC, 'STA201 AND MCM101')
c('MCM330', 'Social Media Strategy', 'استراتيجية وسائل التواصل الاجتماعي', 3, MC, 'MCM240')
c('MCM340', 'Crisis Communication', 'اتصال الأزمات', 3, MC, 'MCM210')
c('MCM350', 'Documentary Production', 'إنتاج الأفلام الوثائقية', 3, MC, 'MCM230')
c('MCM360', 'Media and Society in the UAE', 'الإعلام والمجتمع في الإمارات', 3, MC, 'MCM101 AND GEN101')
c('MCM370', 'Corporate Communication', 'الاتصال المؤسسي', 3, MC, 'MCM210')
c('MCM380', 'Data Journalism', 'صحافة البيانات', 3, MC, 'MCM301 OR MCM320')
c('MCM390', 'Investigative Journalism', 'الصحافة الاستقصائية', 3, MC, 'MCM301')
c('MCM395', 'Event Management', 'إدارة الفعاليات', 3, MC, 'MCM210')
c('MCM401', 'Graduation Project I', 'مشروع التخرج 1', 3, MC, 'MCM320 AND STANDING(Senior)', F)
c('MCM402', 'Graduation Project II', 'مشروع التخرج 2', 4, MC, 'MCM401', SP)
c('MCM410', 'Media Internship', 'التدريب العملي في الإعلام', 4, MC, 'CREDITS(90) AND PERMISSION', SU)

credits = {x[0]: x[3] for x in C}
for x in C:
    for ref in re.findall(r'[A-Z]{2,4}\d{3}L?', x[5]):
        assert ref in credits, (x[0], ref)

GENED = ['ENG101', 'ENG102', 'ARB101', 'ISL101', 'GEN100', 'GEN101', 'GEN110', 'PED101', 'MTH101', 'STA201']
progs = {
    'BIT': dict(name='Bachelor of Information Technology', ar='بكالوريوس تقنية المعلومات', college='College of Engineering and Computing',
                core=['MTH110', 'CSC101', 'CSC101L', 'CSC102', 'CSC201', 'CSC210', 'ITC101', 'ITC110', 'ITC110L', 'ITC201', 'ITC202', 'ITC202L',
                      'ITC210', 'ITC220', 'ITC250', 'ITC301', 'ITC304', 'ITC340', 'ITC401', 'ITC402', 'ITC410', 'GEN120', 'ENG201', 'BUS101'],
                elect=['ITC310', 'ITC320', 'ITC330', 'ITC350', 'ITC360', 'ITC370', 'ITC380', 'ITC390']),
    'BBA-MGT': dict(name='Bachelor of Business Administration in Management', ar='بكالوريوس إدارة الأعمال في الإدارة', college='College of Business',
                    core=['BUS101', 'BUS110', 'BUS120', 'BUS121', 'BUS205', 'BUS210', 'BUS220', 'BUS230', 'BUS301', 'BUS310', 'BUS401', 'BUS410',
                          'ACC101', 'ACC102', 'FIN201', 'FIN301', 'MGT201', 'MGT210', 'MGT220', 'MGT230', 'MGT290', 'MGT301', 'GEN120'],
                    elect=['MGT310', 'MGT320', 'MGT330', 'MGT340', 'MGT350', 'MGT360', 'MGT370', 'MGT380']),
    'BMC': dict(name='Bachelor of Mass Communication', ar='بكالوريوس الاتصال الجماهيري', college='College of Media and Public Relations',
                core=['MCM101', 'MCM110', 'MCM120', 'MCM130', 'MCM130L', 'MCM201', 'MCM210', 'MCM220', 'MCM230', 'MCM240', 'MCM250', 'MCM290',
                      'MCM301', 'MCM310', 'MCM320', 'MCM360', 'MCM401', 'MCM402', 'MCM410', 'GEN120', 'ENG201', 'BUS101'],
                elect=['MCM330', 'MCM340', 'MCM350', 'MCM370', 'MCM380', 'MCM390', 'MCM395']),
}
for k, p in progs.items():
    ge, core = sum(credits[x] for x in GENED), sum(credits[x] for x in p['core'])
    p['free'] = 120 - ge - core - 15
    print(k, 'university', ge, 'major', core, 'electives 15', 'free', p['free'], file=sys.stderr)
    assert p['free'] >= 0 and p['free'] % 3 == 0, (k, p['free'])
    assert not set(GENED) & set(p['core'])


def write(name, header, rows):
    with open(os.path.join(OUT, name), 'w', encoding='utf-8', newline='') as f:
        w = csv.writer(f, lineterminator='\n')
        w.writerow(header)
        w.writerows(rows)


write('courses.csv', ['course_code', 'title', 'title_ar', 'credits', 'department', 'prerequisites', 'offered'], [list(x) for x in C])
write('programs.csv', ['program_code', 'name', 'name_ar', 'college', 'total_credits'],
      [[k, p['name'], p['ar'], p['college'], 120] for k, p in progs.items()])


def sem(code):
    lvl = int(re.search(r'\d{3}', code).group()) // 100
    second = code in ('ENG102', 'MTH110', 'CSC102', 'ACC102', 'BUS121', 'ITC402', 'MCM402', 'BUS401')
    return {1: 1, 2: 3, 3: 5, 4: 7}.get(lvl, 1) + (1 if second else 0)


dp = []
for k, p in progs.items():
    dp += [[k, 2024, 'UNIV', 'University requirements', 'AllOf', '', x, sem(x)] for x in GENED]
    dp += [[k, 2024, 'CORE', 'Major requirements', 'AllOf', '', x, sem(x)] for x in p['core']]
    dp += [[k, 2024, 'MAJ-ELEC', 'Major electives (choose 15 credits)', 'ChooseCredits', 15, x, ''] for x in p['elect']]
    dp.append([k, 2024, 'FREE', f"Free electives ({p['free']} credits)", 'AnyCourse', p['free'], '', ''])
write('degree-plans.csv', ['program_code', 'catalog_year', 'group_key', 'group_name', 'group_type', 'group_credits', 'course_code', 'suggested_semester'], dp)

write('terms.csv', ['term_code', 'name', 'season', 'year', 'start_date', 'end_date', 'teaching_days', 'friday_afternoon', 'is_current'], [
    ['2026FA', 'Fall 2026', 'Fall', 2026, '2026-08-24', '2026-12-18', 'Mon;Tue;Wed;Thu;Fri', 'no', 'no'],
    ['2027SP', 'Spring 2027', 'Spring', 2027, '2027-01-11', '2027-05-07', 'Mon;Tue;Wed;Thu;Fri', 'no', 'yes'],
    ['2027SU', 'Summer 2027', 'Summer', 2027, '2027-06-07', '2027-07-30', 'Mon;Tue;Wed;Thu', 'no', 'no'],
    ['2027FA', 'Fall 2027', 'Fall', 2027, '2027-08-23', '2027-12-17', 'Mon;Tue;Wed;Thu;Fri', 'no', 'no'],
])

# ---- Spring 2027 timetable (UAE patterns: MW, TR, MWF mornings, Mon–Thu evenings; no Sun; nothing after 12:00 Fri)
day_slots = [('Mon;Wed', '09:00', '10:15'), ('Tue;Thu', '09:00', '10:15'), ('Mon;Wed', '11:00', '12:15'),
             ('Tue;Thu', '11:00', '12:15'), ('Mon;Wed', '14:00', '15:15'), ('Tue;Thu', '14:00', '15:15')]
mwf = [('Mon;Wed;Fri', '08:00', '08:50'), ('Mon;Wed;Fri', '09:00', '09:50'), ('Mon;Wed;Fri', '10:00', '10:50'), ('Mon;Wed;Fri', '11:00', '11:50')]
eve = [('Mon;Wed', '17:00', '18:15'), ('Tue;Thu', '17:00', '18:15'), ('Mon;Wed', '18:30', '19:45'), ('Tue;Thu', '18:30', '19:45')]
four_credit_end = {'10:15': '10:50', '12:15': '12:50', '15:15': '15:50', '18:15': '18:50', '19:45': '20:20'}
instructors = ['Dr. Layla Haddad', 'Dr. Yousef Karim', 'Dr. Hana Saleh', 'Dr. Omar Farouk', 'Dr. Reem Nasser',
               'Dr. Tariq Aziz', 'Dr. Salma Idris', 'Dr. Faisal Rahman', 'Dr. Nadia Khoury', 'Dr. Khalid Mansour']
NO_SPRING_SECTIONS = {'ITC390'}  # offered Fall;Spring in the catalog but nothing scheduled: soft-warning case for M3
sec = []
for i, (code, title, ar, cr, dept, pre, off) in enumerate([x for x in C if 'Spring' in x[6] and x[0] not in NO_SPRING_SECTIONS], start=1):
    ins = instructors[i % 10]
    cap = 30 if cr > 1 else 20

    def seats(k):
        return (i * 7 + k * 5) % cap

    def row(section, slot, room, teacher=ins, k=0, delivery='OnCampus', capacity=cap):
        days, start, end = slot
        if cr == 4 and end:
            end = four_credit_end.get(end, end)
        sec.append(['2027SP', code, section, delivery, teacher, room, capacity, seats(k) % capacity, days, start, end])

    if code.endswith('L'):
        # Labs run in the daytime only, so an evenings-only student can't take the lecture+lab pair (M3 test).
        row('L1', ('Wed', '14:00', '16:50'), f'Lab {100 + i}')
        row('L2', ('Thu', '14:00', '16:50'), f'Lab {100 + i}', k=1)
        continue
    if cr == 1:
        row('01', ('Tue', '12:30', '13:20'), f'B{100 + i}')
        continue
    if cr == 2:
        row('01', ('Mon;Wed', '12:30', '13:20'), f'B{100 + i}')
        continue
    row('01', day_slots[i % 6], f'A{100 + i}')
    if dept == GE or code in ('CSC101', 'CSC102', 'ITC201', 'MGT201', 'MCM101', 'BUS101', 'ACC101'):
        second = day_slots[(i + 3) % 6] if cr == 4 else mwf[i % 4]  # 4-credit classes don't fit Friday mornings
        row('02', second, f'A{200 + i}', instructors[(i + 3) % 10], k=1)
    if i % 2 == 0 or code in ('ITC202', 'CSC201', 'ITC301', 'BUS220', 'MGT230', 'MCM210', 'MCM320', 'STA201', 'ENG102'):
        row('E1', eve[i % 4], f'A{300 + i}', instructors[(i + 5) % 10], k=2)
    if i % 5 == 0:
        row('W1', ('', '', ''), '', instructors[(i + 7) % 10], k=3, delivery='Online', capacity=40)

for r in sec:
    if r[8]:
        assert 'Sun' not in r[8], r
        if 'Fri' in r[8]:
            assert r[10] <= '12:00', r
write('sections.csv', ['term_code', 'course_code', 'section', 'delivery', 'instructor', 'room', 'capacity', 'enrolled', 'days', 'start_time', 'end_time'], sec)
print('sections', len(sec), file=sys.stderr)

students = [
    ['202700101', 'Mariam Al Mansoori', '202700101@lu.ac.ae', 'student.new', 'BIT', 2024, '2027SP', 'Good', 'advisor.it'],
    ['202500102', 'Khalid Al Hammadi', '202500102@lu.ac.ae', 'student.probation', 'BBA-MGT', 2024, '2025FA', 'Probation', 'advisor.business'],
    ['202500103', 'Aisha Al Nuaimi', '202500103@lu.ac.ae', 'student.honors', 'BIT', 2024, '2025FA', 'Good', 'advisor.it'],
    ['202300104', 'Omar Al Shamsi', '202300104@lu.ac.ae', 'student.senior', 'BIT', 2024, '2023FA', 'Good', 'advisor.it'],
    ['202500105', 'Noura Al Ketbi', '202500105@lu.ac.ae', 'student.repeat', 'BMC', 2024, '2025FA', 'Good', 'advisor.media'],
    ['202600106', 'Saeed Al Dhaheri', '202600106@lu.ac.ae', '', 'BIT', 2024, '2026FA', 'Good', 'advisor.it'],
    ['202600107', 'Fatima Al Kaabi', '202600107@lu.ac.ae', '', 'BBA-MGT', 2024, '2026FA', 'Good', 'advisor.business'],
    ['202500108', 'Hamad Al Marri', '202500108@lu.ac.ae', '', 'BBA-MGT', 2024, '2025FA', 'Good', 'advisor.business'],
    ['202400109', 'Latifa Al Suwaidi', '202400109@lu.ac.ae', '', 'BMC', 2024, '2024FA', 'Good', 'advisor.media'],
    ['202400110', 'Rashid Al Falasi', '202400110@lu.ac.ae', '', 'BIT', 2024, '2024FA', 'Good', 'advisor.it'],
]
write('students.csv', ['student_id', 'display_name', 'email', 'username', 'program_code', 'catalog_year', 'admit_term', 'status', 'advisor_username'], students)

PTS = {'A': 4, 'A-': 3.7, 'B+': 3.3, 'B': 3, 'B-': 2.7, 'C+': 2.3, 'C': 2, 'C-': 1.7, 'D+': 1.3, 'D': 1, 'F': 0}
tr = []


def add(sid, term, code, grade=None):
    tr.append([sid, term, code, '', grade or '', 'Completed' if grade else 'InProgress', ''])


def add_all(sid, rows):
    for t, code, g in rows:
        add(sid, t, code, g)


def summary(sid):
    order = lambda t: int(t[:4]) * 10 + {'SP': 1, 'SU': 2, 'FA': 3}[t[4:]]
    last = {}
    for r in sorted([r for r in tr if r[0] == sid and r[5] == 'Completed'], key=lambda r: order(r[1])):
        last[r[2]] = r
    num = sum(PTS[r[4]] * credits[r[2]] for r in last.values())
    den = sum(credits[r[2]] for r in last.values())
    earned = sum(credits[r[2]] for r in last.values() if r[4] != 'F')
    earned += sum(credits[r[2]] for r in tr if r[0] == sid and r[5] == 'Transfer')
    return round(num / den + 1e-9, 2) if den else None, earned


# New first-year: only an AP credit for ENG101.
tr.append(['202700101', '', 'ENG101', '', '', 'Transfer', 'Advanced Placement (AP English)'])
# On probation (BBA): GPA below 2.0.
add_all('202500102', [('2025FA', 'ENG101', 'C-'), ('2025FA', 'MTH101', 'D'), ('2025FA', 'BUS101', 'C'), ('2025FA', 'ISL101', 'D+'), ('2025FA', 'GEN100', 'C'),
                      ('2026SP', 'ENG102', 'D'), ('2026SP', 'BUS120', 'F'), ('2026SP', 'ACC101', 'D+'), ('2026SP', 'ARB101', 'C'), ('2026SP', 'PED101', 'C-'),
                      ('2026FA', 'BUS120', 'C-'), ('2026FA', 'MGT201', 'D'), ('2026FA', 'STA201', 'F'), ('2026FA', 'GEN101', 'C')])
# High GPA (BIT): exactly 3.50 on completed work (the 21-credit boundary); Fall 2026 grades not posted yet.
add_all('202500103', [('2025FA', 'ENG101', 'A'), ('2025FA', 'MTH101', 'B'), ('2025FA', 'CSC101', 'A'), ('2025FA', 'CSC101L', 'B'), ('2025FA', 'ITC101', 'B'), ('2025FA', 'GEN100', 'A'),
                      ('2026SP', 'ENG102', 'A'), ('2026SP', 'CSC102', 'B'), ('2026SP', 'MTH110', 'A'), ('2026SP', 'ISL101', 'B'), ('2026SP', 'ITC110', 'A'), ('2026SP', 'ITC110L', 'B'),
                      ('2026SU', 'ARB101', 'A'), ('2026SU', 'PED101', 'C')])
for code in ['CSC201', 'ITC201', 'ITC210', 'STA201', 'GEN101']:
    add('202500103', '2026FA', code)
# Graduating senior (BIT): everything except ITC304 and one major elective: 6 credits left.
bit = progs['BIT']
done = [x for x in GENED + bit['core'] if x != 'ITC304'] + ['ITC310', 'ITC320', 'ITC330', 'ITC350'] + ['MGT201', 'MCM101', 'BUS110'][:bit['free'] // 3]
cycle = ['2023FA', '2024SP', '2024FA', '2025SP', '2025SU', '2025FA', '2026SP', '2026SU', '2026FA']
fixed = {'ITC410': '2025SU', 'ITC401': '2025FA', 'ITC402': '2026SP'}
grades = ['A', 'B+', 'A-', 'B', 'B+', 'A-', 'B', 'A', 'C+', 'B+']
seq = sorted(done, key=lambda x: int(re.search(r'\d{3}', x).group()))
for n, code in enumerate(seq):
    add('202300104', fixed.get(code, cycle[min(n * len(cycle) // len(seq), len(cycle) - 1)]), code, grades[n % len(grades)])
# Failed a prerequisite (BMC): F in MCM201 (Fall only), which MCM301 needs with at least a C.
add_all('202500105', [('2025FA', 'ENG101', 'B'), ('2025FA', 'MCM101', 'B+'), ('2025FA', 'MCM120', 'A-'), ('2025FA', 'ARB101', 'A'), ('2025FA', 'GEN100', 'B'),
                      ('2026SP', 'MCM110', 'B-'), ('2026SP', 'ENG102', 'C+'), ('2026SP', 'MTH101', 'B'), ('2026SP', 'MCM130', 'B'), ('2026SP', 'MCM130L', 'A'),
                      ('2026FA', 'MCM201', 'F'), ('2026FA', 'MCM210', 'B'), ('2026FA', 'STA201', 'C'), ('2026FA', 'GEN101', 'B+'), ('2026FA', 'ISL101', 'A-')])
# Extra students without a sign-in, so advisors have fuller lists.
add_all('202600106', [('2026FA', 'ENG101', 'B'), ('2026FA', 'MTH101', 'B+'), ('2026FA', 'CSC101', 'A-'), ('2026FA', 'CSC101L', 'A'), ('2026FA', 'ITC101', 'B')])
add_all('202600107', [('2026FA', 'ENG101', 'A'), ('2026FA', 'BUS101', 'A-'), ('2026FA', 'MTH101', 'B+'), ('2026FA', 'ISL101', 'A')])
add_all('202500108', [('2025FA', 'ENG101', 'B'), ('2025FA', 'BUS101', 'B'), ('2025FA', 'MTH101', 'C+'), ('2026SP', 'ENG102', 'B'), ('2026SP', 'BUS120', 'B-'),
                      ('2026SP', 'ACC101', 'C'), ('2026FA', 'MGT201', 'B'), ('2026FA', 'STA201', 'C+'), ('2026FA', 'ACC102', 'B-')])
add_all('202400109', [('2024FA', 'ENG101', 'A'), ('2024FA', 'MCM101', 'A'), ('2024FA', 'MCM120', 'A-'), ('2025SP', 'ENG102', 'A-'), ('2025SP', 'MCM110', 'A'),
                      ('2025SP', 'ARB101', 'A'), ('2025FA', 'MCM201', 'A'), ('2025FA', 'MCM210', 'B+'), ('2025FA', 'STA201', 'B'), ('2026SP', 'MCM220', 'A-'),
                      ('2026SP', 'MCM230', 'B+'), ('2026SP', 'MCM240', 'A'), ('2026FA', 'MCM301', 'A-'), ('2026FA', 'MCM320', 'B+'), ('2026FA', 'GEN101', 'A')])
add_all('202400110', [('2024FA', 'ENG101', 'C+'), ('2024FA', 'MTH101', 'B-'), ('2024FA', 'CSC101', 'C'), ('2024FA', 'CSC101L', 'B'), ('2025SP', 'ENG102', 'C+'),
                      ('2025SP', 'CSC102', 'C-'), ('2025SP', 'ITC101', 'B'), ('2025FA', 'CSC201', 'C'), ('2025FA', 'ITC110', 'B-'), ('2025FA', 'ITC110L', 'B'),
                      ('2026SP', 'ITC201', 'C+'), ('2026SP', 'ITC210', 'B'), ('2026FA', 'ITC202', 'C'), ('2026FA', 'ITC202L', 'B-')])
write('transcripts.csv', ['student_id', 'term_code', 'course_code', 'credits', 'grade', 'status', 'source'], tr)

g, e = summary('202500103'); print('honors', g, e, file=sys.stderr); assert g == 3.50
g, e = summary('202500102'); print('probation', g, e, file=sys.stderr); assert g < 2.0
g, e = summary('202300104'); print('senior', g, e, file=sys.stderr); assert e == 114
g, e = summary('202700101'); print('new', g, e, file=sys.stderr); assert e == 3
assert any(r[0] == '202500105' and r[2] == 'MCM201' and r[4] == 'F' for r in tr)
print('all checks passed', file=sys.stderr)
