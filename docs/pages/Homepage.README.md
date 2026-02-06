# Homepage

**Route:** `/`  
**File:** `Homepage.tsx`

## Purpose

Public landing page for Cadence CRM. Designed to convert visitors into users by showcasing the full value proposition, features, and benefits of the platform.

## Page Structure

### Header
- Fixed/sticky navigation with Cadence logo
- Navigation links: Features, How it works, Pricing, FAQ
- Sign In and Get Started CTA buttons

### Hero Section
- Badge: "All-in-one CRM for growing sales teams"
- Main headline: "The CRM that helps you close more deals"
- Subheadline explaining the value proposition
- Two CTAs: "Start Free Trial" and "Watch Demo"
- Trust indicators: Free forever, No credit card, 2-minute setup
- Interactive dashboard mockup with floating notification elements

### Social Proof
- "Trusted by sales teams worldwide" with company logos

### Problem/Solution Section
- Side-by-side comparison of "Without Cadence" vs "With Cadence"
- Highlights pain points and how Cadence solves them

### Core CRM Features
- 9 feature cards in a grid:
  - Lead Management
  - Visual Pipeline
  - Contact Database
  - Company Accounts
  - Task Management
  - Activity Timeline
  - Dashboard & Analytics
  - Intelligent Sales Writer
  - Team Collaboration

### Intelligent Sales Writer Spotlight
- Dark-themed section highlighting the Intelligent Sales Writer feature
- Interactive mockup showing the sales writer form
- List of content types: Sales Emails, Follow-ups, CRM Notes, Deal Messages

### How It Works
- 4-step process:
  1. Create Your Account
  2. Set Your Brand Voice
  3. Import or Add Data
  4. Start Selling

### Use Cases
- 6 use case cards for different roles:
  - Startup Founders
  - Sales Representatives
  - Sales Managers
  - Account Executives
  - Business Development
  - Customer Success

### Testimonials
- 3 customer testimonials with ratings, quotes, and author info

### Pricing
- 3-tier pricing table:
  - Free: $0/month (up to 100 contacts, basic features)
  - Pro: $29/user/month (unlimited, advanced features) - highlighted
  - Enterprise: Custom pricing (SSO, custom integrations, SLA)

### FAQ Section
- Interactive accordion with 6 common questions:
  - How is Cadence different from other CRMs?
  - Do I need any external CRM to use Cadence?
  - How does the Intelligent Sales Writer work?
  - Can I try Cadence for free?
  - Is my data secure?
  - Can my team use Cadence together?

### Security & Trust
- 4 security features: Encrypted Data, Two-Factor Auth, Data Isolation, Self-Hosted Option

### Final CTA
- Orange gradient section with strong call to action
- Two buttons: "Get Started Free" and "See How It Works"

### Footer
- Brand description
- Product links (Dashboard, Leads, Pipeline, etc.)
- Resource links (How It Works, Settings, etc.)
- Legal links (Privacy, Terms, Help)
- Social links (Twitter, LinkedIn, GitHub)
- Copyright

## Components

### FAQItem
Internal accordion component for the FAQ section. Takes `question` and `answer` props.

## Behavior

- Sticky header with blur backdrop on scroll
- Smooth scroll to anchor links (#features, #how-it-works, #pricing, #faq)
- FAQ accordion with expand/collapse animation
- Hover effects on all interactive elements
- Floating elements in hero section with subtle animation
- All CTAs link to `/login` for sign-up/sign-in
- Help/demo links go to `/help`

## API / Data

None. Static content and navigation only.

## Accessibility

- Proper semantic HTML structure (header, main, section, footer, nav)
- ARIA labels on navigation regions
- Focus-visible states on all interactive elements
- Skip link support via `MAIN_CONTENT_ID`
- Proper heading hierarchy (h1 → h2 → h3)
- External links marked with `rel="noopener noreferrer"`

## Animations

- `animate-fade-in` - simple fade in
- `animate-fade-in-up` - fade in with upward movement
- `animate-float` - subtle floating effect on hero elements
- Staggered animation delays for sequential reveal effects
