// Per-page help content for the floating "?" button. Keyed by the first path
// segment (with explicit entries for detail/sub pages). Plain-language copy and
// an emoji "picture" per item so the modal reads as a friendly, illustrated guide.

export interface HelpFeature {
  emoji: string;
  title: string;
  body: string;
}

export interface HelpEntry {
  emoji: string;
  /** Tailwind gradient classes for the hero badge, e.g. "from-indigo-500 to-violet-500". */
  gradient: string;
  title: string;
  tagline: string;
  features: HelpFeature[];
  tips?: string[];
}

const HELP: Record<string, HelpEntry> = {
  dashboard: {
    emoji: '🏠', gradient: 'from-indigo-500 to-violet-500',
    title: 'Dashboard', tagline: 'Your home base — a quick look at how things are going.',
    features: [
      { emoji: '📊', title: 'See your numbers', body: 'Leads, deals and pipeline value, all in one glance.' },
      { emoji: '✍️', title: 'Write with AI', body: 'Create emails and notes in seconds.' },
      { emoji: '🧭', title: 'Go anywhere', body: 'Jump to any part of the app from here.' },
    ],
    tips: ['Use the top menu to move between sections.'],
  },
  leads: {
    emoji: '🧲', gradient: 'from-orange-500 to-amber-500',
    title: 'Leads', tagline: 'New people who might buy from you.',
    features: [
      { emoji: '➕', title: 'Add leads', body: 'One by one, from a file, or automatically from a web form.' },
      { emoji: '🔎', title: 'Find them fast', body: 'Filter and sort the list however you like.' },
      { emoji: '📝', title: 'Log a touch', body: 'Note a call, email or meeting right on the card.' },
      { emoji: '👤', title: 'Pick an owner', body: 'Choose who looks after each lead.' },
      { emoji: '📤', title: 'Download', body: 'Export the list to a spreadsheet.' },
    ],
    tips: ['Click a lead to open everything about it.', 'Tip: press “n” to add, “/” to search.'],
  },
  leadDetail: {
    emoji: '🧑', gradient: 'from-orange-500 to-amber-500',
    title: 'Lead details', tagline: 'Everything about one lead, in one place.',
    features: [
      { emoji: '✏️', title: 'Edit anything', body: 'Change any detail with the editor or right inline.' },
      { emoji: '✉️', title: 'Email & log', body: 'Send an email or note what happened.' },
      { emoji: '👤', title: 'Set the owner', body: 'Decide who handles this lead.' },
      { emoji: '✨', title: 'AI suggests', body: 'A score and the best next thing to do.' },
      { emoji: '🤝', title: 'Convert', body: 'Turn it into a deal, or save it as a contact.' },
    ],
  },
  deals: {
    emoji: '📋', gradient: 'from-emerald-500 to-teal-500',
    title: 'Deals', tagline: 'Your sales board, from first chat to closed.',
    features: [
      { emoji: '🖐️', title: 'Drag to move', body: 'Slide a deal between stages as it progresses.' },
      { emoji: '➕', title: 'Add a deal', body: 'Set its value, owner and close date.' },
      { emoji: '🔎', title: 'Focus', body: 'Filter by owner, value or close date.' },
      { emoji: '💰', title: 'See the total', body: 'Pipeline value updates as you work.' },
    ],
  },
  dealDetail: {
    emoji: '💼', gradient: 'from-emerald-500 to-teal-500',
    title: 'Deal details', tagline: 'Manage one deal from start to finish.',
    features: [
      { emoji: '🛒', title: 'Products', body: 'List what’s being sold and the total adds up.' },
      { emoji: '🧩', title: 'Custom fields', body: 'Fill any extra fields your team set up.' },
      { emoji: '📎', title: 'Attach files', body: 'Keep contracts and docs with the deal.' },
      { emoji: '📝', title: 'Activity & tasks', body: 'Log what happened and track to-dos.' },
    ],
  },
  tasks: {
    emoji: '✅', gradient: 'from-cyan-500 to-blue-500',
    title: 'Tasks', tagline: 'Your to-do list so nothing slips.',
    features: [
      { emoji: '➕', title: 'Add a task', body: 'Give it a due date, reminder and priority.' },
      { emoji: '⏰', title: 'Never miss', body: 'Overdue and due-today tasks get flagged.' },
      { emoji: '☑️', title: 'Track it', body: 'Move tasks from to-do to done.' },
    ],
  },
  contacts: {
    emoji: '🧑‍💼', gradient: 'from-blue-500 to-cyan-500',
    title: 'Contacts', tagline: 'The people you talk to.',
    features: [
      { emoji: '➕', title: 'Add contacts', body: 'Save names, roles and how they like to be reached.' },
      { emoji: '✉️', title: 'Email & log', body: 'Email a contact and keep the history.' },
      { emoji: '📎', title: 'Fields & files', body: 'Add custom fields and attach documents.' },
      { emoji: '📤', title: 'Download', body: 'Export your contacts to a spreadsheet.' },
    ],
  },
  companies: {
    emoji: '🏢', gradient: 'from-violet-500 to-purple-500',
    title: 'Companies', tagline: 'The businesses your contacts work for.',
    features: [
      { emoji: '➕', title: 'Add companies', body: 'Save domain, industry, size and location.' },
      { emoji: '🔗', title: 'See links', body: 'View the contacts and deals at each company.' },
      { emoji: '📤', title: 'Download', body: 'Export your companies to a spreadsheet.' },
    ],
  },
  activities: {
    emoji: '📒', gradient: 'from-amber-500 to-orange-500',
    title: 'Activities', tagline: 'A history of every call, email and note.',
    features: [
      { emoji: '📝', title: 'Log anything', body: 'Record calls, emails, meetings and notes.' },
      { emoji: '🔎', title: 'Filter', body: 'Narrow by type, person or search.' },
      { emoji: '📄', title: 'Save as PDF', body: 'Make a clean, printable report.' },
      { emoji: '📊', title: 'Send to Sheets', body: 'Download a CSV for Google Sheets or Excel.' },
    ],
  },
  sequences: {
    emoji: '🔁', gradient: 'from-indigo-500 to-blue-500',
    title: 'Sequences', tagline: 'Set up follow-ups that run on their own.',
    features: [
      { emoji: '🧱', title: 'Build the steps', body: 'Chain emails, calls, tasks and waits.' },
      { emoji: '👥', title: 'Add people', body: 'Enroll leads and watch their progress.' },
      { emoji: '⏯️', title: 'Turn on/off', body: 'Pause or resume any sequence anytime.' },
    ],
  },
  reports: {
    emoji: '📈', gradient: 'from-indigo-500 to-violet-500',
    title: 'Reports', tagline: 'See how sales and leads are doing.',
    features: [
      { emoji: '💰', title: 'Pipeline & forecast', body: 'Open value, win rate and what’s likely to close.' },
      { emoji: '🧲', title: 'Lead insights', body: 'Conversion rate, the lead funnel and new-lead trend.' },
      { emoji: '📊', title: 'Charts', body: 'Breakdowns by stage, owner, status and source.' },
    ],
  },
  automations: {
    emoji: '⚡', gradient: 'from-amber-500 to-orange-500',
    title: 'Automations', tagline: 'Let the app do repetitive work for you.',
    features: [
      { emoji: '🪝', title: 'When this…', body: 'Pick a trigger, like “a new lead arrives”.' },
      { emoji: '🎯', title: '…do that', body: 'Send an email, make a task, notify someone.' },
      { emoji: '🔌', title: 'On or off', body: 'Switch any rule without deleting it.' },
    ],
  },
  duplicates: {
    emoji: '🧹', gradient: 'from-rose-500 to-pink-500',
    title: 'Duplicates', tagline: 'Tidy up repeated contacts and companies.',
    features: [
      { emoji: '🔍', title: 'Find copies', body: 'We spot likely duplicates for you.' },
      { emoji: '🔗', title: 'Merge them', body: 'Keep one record; the rest fold in.' },
    ],
  },
  audit: {
    emoji: '🧾', gradient: 'from-slate-500 to-slate-600',
    title: 'Audit log', tagline: 'A record of who changed what, and when.',
    features: [
      { emoji: '🕒', title: 'History', body: 'See edits, additions, deletes and more.' },
      { emoji: '🔎', title: 'Filter', body: 'Focus on leads, contacts, companies or deals.' },
    ],
  },
  team: {
    emoji: '👥', gradient: 'from-teal-500 to-emerald-500',
    title: 'Team', tagline: 'The people in your workspace.',
    features: [
      { emoji: '🧑‍🤝‍🧑', title: 'Members', body: 'See everyone and their role.' },
      { emoji: '🎯', title: 'Assigning', body: 'Members can own leads and deals.' },
    ],
  },
  templates: {
    emoji: '🗂️', gradient: 'from-fuchsia-500 to-pink-500',
    title: 'Templates', tagline: 'Starting points for the AI writer.',
    features: [
      { emoji: '✨', title: 'Start faster', body: 'Pick a template to set the goal and tone.' },
      { emoji: '🔎', title: 'Browse', body: 'Find templates by type and category.' },
    ],
  },
  history: {
    emoji: '🕘', gradient: 'from-slate-500 to-slate-600',
    title: 'Copy history', tagline: 'Everything the AI has written for you.',
    features: [
      { emoji: '♻️', title: 'Reuse it', body: 'Find past results and copy them again.' },
    ],
  },
  send: {
    emoji: '📨', gradient: 'from-orange-500 to-rose-500',
    title: 'Send to CRM', tagline: 'Push your AI copy into the right record.',
    features: [
      { emoji: '🎯', title: 'Route content', body: 'Drop a draft onto a contact, lead or deal.' },
    ],
  },
  settings: {
    emoji: '⚙️', gradient: 'from-slate-600 to-slate-700',
    title: 'Settings', tagline: 'Set up the app the way you work.',
    features: [
      { emoji: '✨', title: 'Brand & AI', body: 'Set your voice and tone for the AI writer.' },
      { emoji: '🧩', title: 'Custom fields', body: 'Add your own fields to any record.' },
      { emoji: '📋', title: 'Pipelines', body: 'Customize your deal stages.' },
      { emoji: '🔔', title: 'More', body: 'Notifications, appearance, security and account.' },
    ],
  },
  organizations: {
    emoji: '🏬', gradient: 'from-orange-500 to-amber-500',
    title: 'Workspaces', tagline: 'Switch between or manage your organizations.',
    features: [
      { emoji: '🏢', title: 'Workspaces', body: 'Each one has its own leads, deals and team.' },
    ],
  },
  leadWebhook: {
    emoji: '🌐', gradient: 'from-orange-500 to-amber-500',
    title: 'Lead webhook', tagline: 'Auto-capture leads from your website or forms.',
    features: [
      { emoji: '🔗', title: 'Your inbox URL', body: 'Send form data here and a lead is created.' },
      { emoji: '🧪', title: 'Test it', body: 'Try a sample to see how data maps in.' },
    ],
  },
  leadImport: {
    emoji: '📥', gradient: 'from-orange-500 to-amber-500',
    title: 'Import leads', tagline: 'Bring in lots of leads from a spreadsheet.',
    features: [
      { emoji: '📄', title: 'Upload a CSV', body: 'Drop in your file to get started.' },
      { emoji: '🔀', title: 'Match columns', body: 'Line up your columns with lead fields.' },
    ],
  },
};

const DEFAULT_HELP: HelpEntry = {
  emoji: '💡', gradient: 'from-indigo-500 to-violet-500',
  title: 'About this page', tagline: 'Here’s what you can do here.',
  features: [
    { emoji: '🧭', title: 'Get around', body: 'Use the top menu to move between sections.' },
    { emoji: '🔎', title: 'Find anything', body: 'Search for contacts, companies and deals.' },
  ],
};

/** Resolve the help entry for a pathname (handles sub-pages and /section/:id detail pages). */
export function resolvePageHelp(pathname: string): HelpEntry {
  if (pathname === '/leads/webhook') return HELP.leadWebhook ?? DEFAULT_HELP;
  if (pathname === '/leads/import') return HELP.leadImport ?? DEFAULT_HELP;
  if (/^\/leads\/[^/]+$/.test(pathname)) return HELP.leadDetail ?? DEFAULT_HELP;
  if (/^\/deals\/[^/]+$/.test(pathname)) return HELP.dealDetail ?? DEFAULT_HELP;
  if (/^\/contacts\/[^/]+$/.test(pathname)) return HELP.contacts ?? DEFAULT_HELP;
  if (/^\/companies\/[^/]+$/.test(pathname)) return HELP.companies ?? DEFAULT_HELP;
  if (/^\/tasks\/[^/]+$/.test(pathname)) return HELP.tasks ?? DEFAULT_HELP;
  const seg = pathname.split('/').filter(Boolean)[0] || 'dashboard';
  return HELP[seg] ?? DEFAULT_HELP;
}
