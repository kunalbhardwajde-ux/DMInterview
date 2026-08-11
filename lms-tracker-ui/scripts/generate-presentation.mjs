import path from 'node:path'
import { fileURLToPath } from 'node:url'
import PptxGenJS from 'pptxgenjs'

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

const rootDir = path.resolve(__dirname, '..', '..')
const assetsDir = path.join(rootDir, 'presentation-assets')
const outputPath = path.join(rootDir, 'LMS-Smart-Presentation.pptx')

const image1 = path.join(assetsDir, 'slide-01-manager-overview.png')
const image2 = path.join(assetsDir, 'slide-02-skill-priority.png')
const image3 = path.join(assetsDir, 'slide-03-skill-expanded.png')
const image4 = path.join(assetsDir, 'slide-04-learner-portal.png')

const pptx = new PptxGenJS()
pptx.layout = 'LAYOUT_WIDE'
pptx.author = 'Kunal'
pptx.company = 'Interview Submission'
pptx.subject = 'LMS Tracking System'
pptx.title = 'LMS Pro Suite - System Design and Skill Prioritization'
pptx.lang = 'en-US'

const COLORS = {
  bg: 'F4F7FB',
  title: '0F172A',
  body: '334155',
  accent: '1D5FD6',
  soft: 'E2E8F0',
}

function addSlideHeader(slide, title, subtitle = '') {
  slide.background = { color: COLORS.bg }
  slide.addShape(pptx.ShapeType.rect, {
    x: 0,
    y: 0,
    w: 13.33,
    h: 0.9,
    fill: { color: 'FFFFFF' },
    line: { color: 'FFFFFF' },
  })

  slide.addText(title, {
    x: 0.5,
    y: 0.18,
    w: 8.6,
    h: 0.45,
    bold: true,
    color: COLORS.title,
    fontFace: 'Aptos Display',
    fontSize: 24,
  })

  if (subtitle) {
    slide.addText(subtitle, {
      x: 0.5,
      y: 0.62,
      w: 10.8,
      h: 0.3,
      color: COLORS.body,
      fontFace: 'Aptos',
      fontSize: 12,
    })
  }
}

function addBullets(slide, lines, x = 0.7, y = 1.3, w = 12, h = 5.6) {
  const runs = lines.map((line) => ({ text: line, options: { bullet: { indent: 14 } } }))
  slide.addText(runs, {
    x,
    y,
    w,
    h,
    color: COLORS.body,
    fontFace: 'Aptos',
    fontSize: 20,
    breakLine: true,
    paraSpaceAfterPt: 14,
    valign: 'top',
  })
}

let slide = pptx.addSlide()
addSlideHeader(slide, 'LMS Pro Suite', 'From learning operations to interview-ready skill prioritization')
slide.addText('Complex Problem -> Built System -> Explainable Decisions', {
  x: 0.7,
  y: 1.7,
  w: 12,
  h: 0.5,
  color: COLORS.accent,
  bold: true,
  fontFace: 'Aptos Display',
  fontSize: 24,
})
addBullets(slide, [
  'Problem: random candidate interviews cause poor project-fit outcomes.',
  'Solution: rank employees using completed-course evidence and organizational scope.',
  'Tech: ASP.NET Core modular API + SQL Server + React with mock or real API modes.',
])

slide = pptx.addSlide()
addSlideHeader(slide, '1) Problem Framing', 'Why this is hard and why a full system is needed')
addBullets(slide, [
  'One dataset must serve leaders, team managers, and individual learners differently.',
  'Mandatory compliance, deadlines, and provider progress sync must remain consistent.',
  'Staffing must become evidence-based, not manager memory or random selection.',
  'This requires policy modeling, scoped analytics, and integration-safe workflows.',
])

slide = pptx.addSlide()
addSlideHeader(slide, '2) System Design', 'Modular monolith architecture with future microservice path')
addBullets(slide, [
  'Composition root configures SQL Server, CORS, integration clients, and endpoint modules.',
  'Feature modules: departments, teams, learners, courses, assignments, dashboard, reports, integrations.',
  'Code-first EF constraints enforce data integrity (unique employee code/email, provider course identity).',
  'Frontend API adapter allows one-click switch between in-memory mock and real backend endpoints.',
])

slide.addShape(pptx.ShapeType.roundRect, {
  x: 0.7,
  y: 4.9,
  w: 11.9,
  h: 1.6,
  fill: { color: 'FFFFFF' },
  line: { color: COLORS.soft },
  radius: 0.08,
})
slide.addText('Data flow: React UI -> API adapter -> (Mock engine OR Backend API) -> SQL + Provider integrations', {
  x: 1.0,
  y: 5.35,
  w: 11.3,
  h: 0.8,
  color: COLORS.accent,
  bold: true,
  fontFace: 'Aptos',
  fontSize: 16,
  align: 'center',
  valign: 'mid',
})

slide = pptx.addSlide()
addSlideHeader(slide, 'Manager Command Center', 'Scope filters, KPIs, assignment operations, progress and compliance')
slide.addImage({ path: image1, x: 0.55, y: 1.15, w: 12.2, h: 5.8 })

slide = pptx.addSlide()
addSlideHeader(slide, 'Skill-Based Interview Priority', 'Turn project skill keywords into ranked candidate shortlists')
slide.addImage({ path: image2, x: 0.55, y: 1.15, w: 12.2, h: 5.8 })

slide = pptx.addSlide()
addSlideHeader(slide, 'Explainability: Expanded Candidate Details', 'Per employee, show complete matched skills and matched completed courses')
slide.addImage({ path: image3, x: 0.55, y: 1.15, w: 12.2, h: 5.8 })

slide = pptx.addSlide()
addSlideHeader(slide, 'Learner Portal Personas', 'Individual privacy scope and team-manager monitoring scope')
slide.addImage({ path: image4, x: 0.55, y: 1.15, w: 12.2, h: 5.8 })

slide = pptx.addSlide()
addSlideHeader(slide, '3) Tradeoffs and Decisions', 'What was chosen, alternatives, and what would change')
addBullets(slide, [
  'Modular monolith now, extraction to microservices later for speed and maintainability.',
  'Keyword scoring chosen for explainability and fast delivery; semantic matching is next iteration.',
  'On-demand sync endpoints simplify MVP; production should move to background workers and retries.',
  'Mock mode ensures demo continuity; contract tests are needed to prevent drift over time.',
])

slide = pptx.addSlide()
addSlideHeader(slide, '4) Failure Modes and Behavior', 'How the system degrades and recovers')
addBullets(slide, [
  'Provider sync unavailable: core LMS still operates with existing assignments and local/mock data.',
  'Invalid operations blocked by validation and DB constraints (duplicate identities, bad targets).',
  'No exact skill keyword match: shortlist still returns ranked employees (lower priority tiers).',
  'Backend downtime: frontend can run in mock mode for uninterrupted demo and validation.',
])

slide = pptx.addSlide()
addSlideHeader(slide, 'Roadmap and Production Readiness', 'Next steps beyond MVP')
addBullets(slide, [
  'Add authentication/authorization and audit trails for assignment and scoring actions.',
  'Introduce skill taxonomy + embeddings for semantic role-to-skill mapping.',
  'Add background sync scheduler, queue-based retries, and observability dashboards.',
  'Publish weighted scoring controls for hiring leads and project managers.',
])

await pptx.writeFile({ fileName: outputPath })
console.log(`Presentation generated: ${outputPath}`)
