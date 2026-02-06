using ACI.Application.Interfaces;

namespace ACI.Infrastructure.Services;

/// <summary>
/// Template-based Intelligent Sales Writer with full coverage of all type+goal combinations
/// and brand tone support. Falls back to templates when AI is not configured.
/// </summary>
public sealed class TemplateCopyGenerator : ICopyGenerator
{
    // Template structure: [copyTypeId|goal|brandTone] = template
    // brandTone: professional (default), friendly, persuasive
    private static readonly Dictionary<string, string> Templates = new(StringComparer.OrdinalIgnoreCase)
    {
        // ============================================
        // SALES EMAIL TEMPLATES
        // ============================================
        
        // Schedule a meeting
        ["sales-email|Schedule a meeting|professional"] = """
            Hi [First Name],

            I hope this email finds you well. I wanted to reach out to see if you'd be interested in scheduling a brief call to discuss how our solutions can help [Company Name] achieve your goals.

            We've helped companies similar to yours:
            • Increase productivity by 40%
            • Reduce operational costs by 25%
            • Streamline workflows and save time

            Would you be available for a 15-minute call next week? I'd love to learn more about your current challenges and share how we can help.

            Looking forward to connecting.

            Best regards,
            [Your Name]
            """,
        ["sales-email|Schedule a meeting|friendly"] = """
            Hey [First Name]! 👋

            Hope you're having a great week! I wanted to reach out because I think we could really help [Company Name] hit some big goals.

            Quick wins we've helped other teams achieve:
            • 40% productivity boost 🚀
            • 25% cost savings 💰
            • Way smoother workflows

            Would you be up for a quick 15-minute chat next week? I'd love to hear what you're working on!

            Talk soon,
            [Your Name]
            """,
        ["sales-email|Schedule a meeting|persuasive"] = """
            Hi [First Name],

            I'll be direct: companies like [Company Name] are leaving significant value on the table, and I'd like to show you how to capture it.

            Our clients typically see:
            • 40% productivity gains within 90 days
            • 25% cost reduction in the first quarter
            • ROI that pays for itself in weeks, not months

            I have 15 minutes next week that could change how you think about [relevant area]. Are you available?

            The sooner we connect, the sooner you'll see results.

            Best,
            [Your Name]
            """,

        // Follow up after demo
        ["sales-email|Follow up after demo|professional"] = """
            Hi [First Name],

            Thank you for taking the time to see our demo. I wanted to follow up with a quick summary and outline our next steps.

            Based on our conversation, here's what I believe could help [Company Name]:
            • [Key benefit 1 discussed in demo]
            • [Key benefit 2 discussed in demo]
            • [Key benefit 3 discussed in demo]

            I'd love to schedule a follow-up call to answer any questions and discuss implementation. Would [Day/Time] work for you?

            Best regards,
            [Your Name]
            """,
        ["sales-email|Follow up after demo|friendly"] = """
            Hey [First Name]! 😊

            Thanks so much for your time on the demo — it was great chatting with you!

            I'm really excited about what we discussed for [Company Name]:
            • [Benefit 1] — this one seemed like a big win for you
            • [Benefit 2] — could save your team tons of time
            • [Benefit 3] — the cherry on top!

            Let's find time to chat through next steps. When works best for you?

            Talk soon!
            [Your Name]
            """,
        ["sales-email|Follow up after demo|persuasive"] = """
            Hi [First Name],

            After our demo, I'm confident we can deliver exceptional results for [Company Name].

            Here's what stood out:
            • [Benefit 1] — this alone could justify the investment
            • [Benefit 2] — your competitors are already doing this
            • [Benefit 3] — the faster you move, the faster you'll see ROI

            I've reserved time for a follow-up this week. Can you confirm [Day/Time]? Every day of delay is leaving value on the table.

            Let's make this happen.

            Best,
            [Your Name]
            """,

        // Request feedback
        ["sales-email|Request feedback|professional"] = """
            Hi [First Name],

            I hope you're doing well. I wanted to reach out to gather your feedback on [product/service/proposal].

            Your insights would be incredibly valuable as we continue to improve. Specifically, I'd love to know:
            • What aspects resonated most with you?
            • Are there any concerns or questions we haven't addressed?
            • What would make this decision easier for you?

            Would you have a few minutes this week to share your thoughts?

            Thank you for your time.

            Best regards,
            [Your Name]
            """,
        ["sales-email|Request feedback|friendly"] = """
            Hey [First Name]! 👋

            Hope things are going well! I wanted to check in and get your honest thoughts on what we discussed.

            No pressure at all — just curious:
            • What did you think overall?
            • Anything that didn't quite click?
            • What would make this a no-brainer for you?

            Your feedback really helps us get better. Got 5 minutes to chat?

            Thanks!
            [Your Name]
            """,
        ["sales-email|Request feedback|persuasive"] = """
            Hi [First Name],

            Your opinion matters, and I'd value your candid feedback.

            We're at a pivotal point, and understanding your perspective will help us:
            • Tailor the solution specifically to [Company Name]'s needs
            • Address any concerns before they become roadblocks
            • Move forward with confidence on both sides

            Five minutes of your time now could save weeks later. When can we connect?

            Best,
            [Your Name]
            """,

        // Share resources
        ["sales-email|Share resources|professional"] = """
            Hi [First Name],

            Following up on our conversation, I wanted to share some resources that I think you'll find valuable:

            • [Resource 1] — Relevant case study from your industry
            • [Resource 2] — Guide to [topic discussed]
            • [Resource 3] — ROI calculator for your specific use case

            Please let me know if you have any questions about these materials, or if there's anything specific you'd like me to send over.

            Best regards,
            [Your Name]
            """,
        ["sales-email|Share resources|friendly"] = """
            Hey [First Name]! 📚

            As promised, here are those resources I mentioned:

            • [Resource 1] — Think you'll really like this one!
            • [Resource 2] — Super relevant to what we talked about
            • [Resource 3] — Play around with this when you have a sec

            Let me know what you think! Happy to walk through any of these together.

            Cheers,
            [Your Name]
            """,
        ["sales-email|Share resources|persuasive"] = """
            Hi [First Name],

            I've put together some materials that directly address the challenges you mentioned:

            • [Resource 1] — See how a similar company achieved 40% gains
            • [Resource 2] — The exact framework we'd implement for you
            • [Resource 3] — Calculate your potential ROI (spoiler: it's significant)

            These aren't generic marketing materials — they're specifically chosen for [Company Name]. Worth 10 minutes of your time.

            Best,
            [Your Name]
            """,

        // Check in on progress
        ["sales-email|Check in on progress|professional"] = """
            Hi [First Name],

            I wanted to check in and see how things are progressing on your end regarding [topic/project we discussed].

            Please let me know if:
            • You have any questions I can help answer
            • There are any internal discussions I can support
            • You need any additional information or resources

            I'm here to help in any way I can.

            Best regards,
            [Your Name]
            """,
        ["sales-email|Check in on progress|friendly"] = """
            Hey [First Name]! 👋

            Just wanted to pop in and see how everything's going with [topic/project]!

            No rush on anything — just checking if:
            • You've had a chance to review things?
            • Any questions came up?
            • There's anything I can help with?

            Let me know how you're doing!

            [Your Name]
            """,
        ["sales-email|Check in on progress|persuasive"] = """
            Hi [First Name],

            I wanted to touch base on [topic/project]. Time-sensitive opportunities don't wait, and I want to make sure [Company Name] doesn't miss out.

            Quick questions:
            • Where are you in the decision process?
            • What's standing between you and moving forward?
            • How can I help accelerate this?

            Let's reconnect this week and keep momentum going.

            Best,
            [Your Name]
            """,

        // Close the deal
        ["sales-email|Close the deal|professional"] = """
            Hi [First Name],

            I wanted to follow up on our proposal and see where things stand for [Company Name].

            To summarize what we've agreed upon:
            • [Key point 1]
            • [Key point 2]
            • [Investment/pricing discussed]

            I've attached the final agreement for your review. Please let me know if you have any questions or if you're ready to move forward.

            Looking forward to partnering with you.

            Best regards,
            [Your Name]
            """,
        ["sales-email|Close the deal|friendly"] = """
            Hey [First Name]! 🎉

            So excited we're at the finish line! Here's a quick recap of what we're moving forward with:

            • [Key point 1]
            • [Key point 2]
            • [Investment/pricing]

            I've attached everything you need. Just need your sign-off and we're good to go!

            Can't wait to get started!

            [Your Name]
            """,
        ["sales-email|Close the deal|persuasive"] = """
            Hi [First Name],

            We've covered all the bases, and the path forward is clear. Here's where we stand:

            • [Key point 1] — delivering [specific value]
            • [Key point 2] — starting [timeframe]
            • Investment: [amount] — ROI expected within [timeframe]

            The agreement is attached and ready for signature. Every week of delay costs [Company Name] approximately [estimated lost value].

            Let's finalize this today. What do you need from me to make that happen?

            Best,
            [Your Name]
            """,

        // ============================================
        // FOLLOW-UP TEMPLATES
        // ============================================

        // Schedule a meeting
        ["follow-up|Schedule a meeting|professional"] = """
            Hi [First Name],

            I wanted to follow up on my previous message about scheduling a call.

            I understand you're busy, so I'll keep this brief: I'd love 15 minutes to discuss how we can help [Company Name] achieve [specific goal].

            Would any of these times work for you?
            • [Option 1]
            • [Option 2]
            • [Option 3]

            Best regards,
            [Your Name]
            """,
        ["follow-up|Schedule a meeting|friendly"] = """
            Hey [First Name]! 👋

            Just bumping this up in your inbox — would love to find time to chat!

            I know calendars get crazy, so here are a few options:
            • [Option 1]
            • [Option 2]
            • [Option 3]

            Any of those work? Or just let me know what's better for you!

            [Your Name]
            """,
        ["follow-up|Schedule a meeting|persuasive"] = """
            Hi [First Name],

            I'm following up because I genuinely believe this conversation could impact [Company Name]'s results this quarter.

            15 minutes. That's all I'm asking. Here's my availability:
            • [Option 1]
            • [Option 2]
            • [Option 3]

            If none of these work, name your time. This is worth prioritizing.

            Best,
            [Your Name]
            """,

        // Follow up after demo
        ["follow-up|Follow up after demo|professional"] = """
            Hi [First Name],

            Thank you again for the demo. I wanted to follow up on our conversation and the next steps we discussed.

            As promised, here's [resource/recap]. I'm happy to schedule another call if you'd like to dive deeper into any area.

            When would be a good time to connect again?

            Best regards,
            [Your Name]
            """,
        ["follow-up|Follow up after demo|friendly"] = """
            Hey [First Name]! 😊

            Thanks again for the great conversation! Here's that [resource/info] I promised.

            Let me know if any questions pop up — happy to jump on another call anytime!

            What does your week look like?

            [Your Name]
            """,
        ["follow-up|Follow up after demo|persuasive"] = """
            Hi [First Name],

            Following our demo, I wanted to make sure you have everything needed to move forward.

            Attached: [resource/recap with key takeaways]

            The solutions we discussed could start delivering value for [Company Name] within weeks. Let's schedule our next conversation while the details are fresh.

            What's your availability this week?

            Best,
            [Your Name]
            """,

        // Request feedback
        ["follow-up|Request feedback|professional"] = """
            Hi [First Name],

            I'm following up on my request for feedback. Your input would be incredibly helpful as we finalize [proposal/solution] for [Company Name].

            Even a brief response would be valuable. Is there a better time to connect?

            Thank you for your time.

            Best regards,
            [Your Name]
            """,
        ["follow-up|Request feedback|friendly"] = """
            Hey [First Name]! 👋

            Just checking in — did you get a chance to think about what we discussed?

            Would love to hear your thoughts, even if it's just a quick "looks good" or "I have questions"!

            Let me know!

            [Your Name]
            """,
        ["follow-up|Request feedback|persuasive"] = """
            Hi [First Name],

            I'm reaching out again because your feedback is crucial to moving forward.

            Without your input, I can't tailor the solution to [Company Name]'s specific needs. Five minutes of your time now prevents weeks of back-and-forth later.

            Can we connect today?

            Best,
            [Your Name]
            """,

        // Share resources
        ["follow-up|Share resources|professional"] = """
            Hi [First Name],

            I wanted to follow up on the resources I shared. Have you had a chance to review them?

            I'd be happy to walk through any of the materials together or answer questions.

            Please let me know if there's anything else I can provide.

            Best regards,
            [Your Name]
            """,
        ["follow-up|Share resources|friendly"] = """
            Hey [First Name]! 📚

            Just checking if you had a chance to look at those resources I sent over?

            No worries if you haven't yet — let me know if you want me to highlight the key parts!

            [Your Name]
            """,
        ["follow-up|Share resources|persuasive"] = """
            Hi [First Name],

            Quick follow-up on the resources I shared. The case study in particular shows results directly relevant to [Company Name].

            The companies profiled faced the same challenges you mentioned. Their results speak for themselves.

            Worth 10 minutes of your time. When can we discuss?

            Best,
            [Your Name]
            """,

        // Check in on progress
        ["follow-up|Check in on progress|professional"] = """
            Hi [First Name],

            I wanted to follow up and see if there have been any updates on your end.

            Is there anything I can help with to keep things moving forward?

            Please let me know how I can best support you.

            Best regards,
            [Your Name]
            """,
        ["follow-up|Check in on progress|friendly"] = """
            Hey [First Name]! 👋

            Just wanted to check in — any updates on your end?

            Happy to help with anything that's come up!

            [Your Name]
            """,
        ["follow-up|Check in on progress|persuasive"] = """
            Hi [First Name],

            I'm following up because timing matters. Where do things stand?

            If there are roadblocks, let's address them now. If you're ready to move forward, I can have everything prepared by [date].

            What's the status?

            Best,
            [Your Name]
            """,

        // Close the deal
        ["follow-up|Close the deal|professional"] = """
            Hi [First Name],

            I wanted to follow up on the proposal I sent. Do you have any questions or concerns I can address?

            I'm confident we can finalize the details and get started whenever you're ready.

            Please let me know how I can help move this forward.

            Best regards,
            [Your Name]
            """,
        ["follow-up|Close the deal|friendly"] = """
            Hey [First Name]! 🎯

            Just checking in on the proposal — any questions I can answer?

            Really looking forward to working together!

            [Your Name]
            """,
        ["follow-up|Close the deal|persuasive"] = """
            Hi [First Name],

            I'm following up because we're at a decision point.

            The proposal is ready. The value is clear. The only thing missing is your go-ahead.

            What's holding things up? Let's address it today and get [Company Name] moving toward results.

            Best,
            [Your Name]
            """,

        // ============================================
        // CRM NOTE TEMPLATES
        // ============================================

        // Schedule a meeting
        ["crm-note|Schedule a meeting|professional"] = """
            Meeting Request – [Contact Name] – [Date]

            Status: Awaiting response on meeting request

            Outreach:
            - Sent initial meeting request via email
            - Proposed times: [times offered]
            - Purpose: [discuss topic/demo/etc.]

            Next Steps:
            - [ ] Follow up if no response by [date]
            - [ ] Prepare agenda once confirmed
            """,
        ["crm-note|Schedule a meeting|friendly"] = """
            📅 Meeting request sent to [Contact Name]

            Reached out to schedule a call — waiting to hear back!

            Proposed: [times]
            Topic: [what we'll discuss]

            To do:
            - Follow up [date] if no reply
            - Get agenda ready once confirmed
            """,
        ["crm-note|Schedule a meeting|persuasive"] = """
            MEETING REQUEST – [Contact Name]

            Sent: [Date]
            Status: PENDING RESPONSE

            Key Points Emphasized:
            - Time-sensitive opportunity
            - ROI potential for [Company Name]
            - Competitive urgency

            ACTION REQUIRED:
            - [ ] Follow up within 48 hours
            - [ ] Prepare value-focused agenda
            """,

        // Follow up after demo
        ["crm-note|Follow up after demo|professional"] = """
            Demo Follow-up – [Contact Name] – [Date]

            Demo Summary:
            - Attendees: [names]
            - Duration: [time]
            - Products shown: [features demonstrated]

            Key Interests:
            - [Interest 1]
            - [Interest 2]

            Concerns Raised:
            - [Concern 1]
            - [Concern 2]

            Next Steps:
            - [ ] Send follow-up email with recap
            - [ ] Share requested resources
            - [ ] Schedule next call for [date]
            """,
        ["crm-note|Follow up after demo|friendly"] = """
            🎬 Demo with [Contact Name] — [Date]

            Great call! Here's what happened:

            They loved:
            - [Feature/benefit 1]
            - [Feature/benefit 2]

            Questions/concerns:
            - [Item 1]
            - [Item 2]

            Next up:
            - Send recap + resources
            - Schedule follow-up call
            """,
        ["crm-note|Follow up after demo|persuasive"] = """
            DEMO COMPLETED – [Contact Name] – [Date]

            HOT LEAD INDICATORS:
            ✓ Asked detailed implementation questions
            ✓ Mentioned budget availability
            ✓ Introduced additional stakeholders

            OBJECTIONS TO ADDRESS:
            - [Objection 1] → Response: [how to handle]
            - [Objection 2] → Response: [how to handle]

            IMMEDIATE ACTIONS:
            - [ ] Send compelling follow-up within 24 hours
            - [ ] Loop in [decision maker] for next call
            - [ ] Prepare ROI analysis
            """,

        // Request feedback
        ["crm-note|Request feedback|professional"] = """
            Feedback Request – [Contact Name] – [Date]

            Summary:
            - Requested feedback on [proposal/product/demo]
            - Key areas: [what we asked about]

            Response Status: [Pending/Received]

            Feedback Received:
            - [Point 1]
            - [Point 2]

            Action Items:
            - [ ] [Response action]
            - [ ] [Follow-up action]
            """,
        ["crm-note|Request feedback|friendly"] = """
            💬 Asked [Contact Name] for feedback

            What we asked about:
            - [Topic 1]
            - [Topic 2]

            Their thoughts:
            - [Feedback point 1]
            - [Feedback point 2]

            Next:
            - [Action based on feedback]
            """,
        ["crm-note|Request feedback|persuasive"] = """
            FEEDBACK REQUEST – [Contact Name]

            Date: [Date]
            Status: [Awaiting/Received]

            Strategic Purpose:
            - Uncover hidden objections
            - Identify decision criteria
            - Accelerate timeline

            Key Insights Gathered:
            - [Insight 1 — implication]
            - [Insight 2 — implication]

            NEXT MOVES:
            - [ ] Address concerns in next touchpoint
            - [ ] Adjust positioning based on feedback
            """,

        // Share resources
        ["crm-note|Share resources|professional"] = """
            Resources Shared – [Contact Name] – [Date]

            Materials Sent:
            - [Resource 1] — [description]
            - [Resource 2] — [description]
            - [Resource 3] — [description]

            Context: [Why these were relevant]

            Follow-up Plan:
            - [ ] Check if received/reviewed by [date]
            - [ ] Offer walkthrough if needed
            """,
        ["crm-note|Share resources|friendly"] = """
            📎 Sent resources to [Contact Name]

            What I shared:
            - [Resource 1]
            - [Resource 2]
            - [Resource 3]

            Why: [Relevant to their situation]

            Will check in [date] to see if helpful!
            """,
        ["crm-note|Share resources|persuasive"] = """
            RESOURCES DEPLOYED – [Contact Name]

            Targeted Materials:
            - [Resource 1] — addresses [specific objection]
            - [Resource 2] — demonstrates [proof point]
            - [Resource 3] — enables [internal selling]

            Strategic Intent:
            Enable champion to build internal business case

            FOLLOW-UP:
            - [ ] Confirm receipt within 24 hours
            - [ ] Offer to present to stakeholders
            """,

        // Check in on progress
        ["crm-note|Check in on progress|professional"] = """
            Progress Check – [Contact Name] – [Date]

            Current Status: [where things stand]

            Updates from Contact:
            - [Update 1]
            - [Update 2]

            Blockers Identified:
            - [Blocker 1]
            - [Blocker 2]

            Next Steps:
            - [ ] [Action to address blockers]
            - [ ] Follow up by [date]
            """,
        ["crm-note|Check in on progress|friendly"] = """
            ✅ Checked in with [Contact Name]

            Where things stand:
            - [Status update]

            What's happening on their end:
            - [Update 1]
            - [Update 2]

            Next check-in: [date]
            """,
        ["crm-note|Check in on progress|persuasive"] = """
            DEAL STATUS CHECK – [Contact Name]

            Date: [Date]
            Deal Stage: [stage]
            Health: [Green/Yellow/Red]

            PROGRESS INDICATORS:
            ✓/✗ Budget confirmed
            ✓/✗ Timeline established
            ✓/✗ Decision makers engaged
            ✓/✗ Competition identified

            BLOCKERS:
            - [Blocker] → Mitigation: [plan]

            ACCELERATION ACTIONS:
            - [ ] [Urgent action]
            - [ ] [Secondary action]
            """,

        // Close the deal
        ["crm-note|Close the deal|professional"] = """
            Closing Activities – [Contact Name] – [Date]

            Deal Summary:
            - Value: [amount]
            - Products: [what's included]
            - Timeline: [implementation dates]

            Status: [Proposal sent/Negotiating/Awaiting signature]

            Outstanding Items:
            - [ ] [Item 1]
            - [ ] [Item 2]

            Expected Close Date: [date]
            """,
        ["crm-note|Close the deal|friendly"] = """
            🎉 Almost there with [Contact Name]!

            Deal details:
            - Worth: [amount]
            - Includes: [products/services]
            - Starting: [date]

            Just need:
            - [Outstanding item 1]
            - [Outstanding item 2]

            Should close by: [date] 🤞
            """,
        ["crm-note|Close the deal|persuasive"] = """
            CLOSING – [Contact Name] – [Company Name]

            DEAL VALUE: [amount]
            CLOSE PROBABILITY: [%]
            TARGET CLOSE: [date]

            FINAL REQUIREMENTS:
            - [ ] [Requirement 1] — Owner: [who]
            - [ ] [Requirement 2] — Owner: [who]

            COMPETITIVE RISKS:
            - [Risk] → Counter: [action]

            EXECUTIVE ESCALATION:
            [Available/Not needed] — Trigger if no movement by [date]
            """,

        // ============================================
        // DEAL MESSAGE TEMPLATES
        // ============================================

        // Schedule a meeting
        ["deal-message|Schedule a meeting|professional"] = """
            Hi [First Name],

            I wanted to schedule a meeting to discuss the next steps for our partnership with [Company Name].

            Topics to cover:
            • Current status and timeline
            • Any outstanding questions
            • Implementation planning

            Would [Day/Time] work for you?

            Best regards,
            [Your Name]
            """,
        ["deal-message|Schedule a meeting|friendly"] = """
            Hey [First Name]! 👋

            Let's get a meeting on the calendar to keep things moving with [Company Name]!

            Quick agenda:
            • Where we're at
            • Any questions
            • What's next

            When works for you?

            [Your Name]
            """,
        ["deal-message|Schedule a meeting|persuasive"] = """
            Hi [First Name],

            We're at a critical juncture with [Company Name], and I'd like to schedule a meeting to ensure we maintain momentum.

            Agenda:
            • Finalize outstanding details
            • Confirm decision timeline
            • Remove any remaining obstacles

            I have availability [times]. This meeting is essential to keeping us on track for [target close date].

            Best,
            [Your Name]
            """,

        // Follow up after demo
        ["deal-message|Follow up after demo|professional"] = """
            Hi [First Name],

            Following up on the demo we completed with [Company Name]. I wanted to share next steps and gather any feedback from stakeholders.

            Key takeaways from the demo:
            • [Takeaway 1]
            • [Takeaway 2]

            Please let me know if there are additional questions or if we should schedule time with other team members.

            Best regards,
            [Your Name]
            """,
        ["deal-message|Follow up after demo|friendly"] = """
            Hey [First Name]! 🎬

            Great demo with [Company Name]! Wanted to check in and see how everyone felt about it.

            Main points we covered:
            • [Point 1]
            • [Point 2]

            Any feedback from the team? Happy to do another session if helpful!

            [Your Name]
            """,
        ["deal-message|Follow up after demo|persuasive"] = """
            Hi [First Name],

            The demo with [Company Name] highlighted significant alignment. Let's capitalize on this momentum.

            Value demonstrated:
            • [Value point 1]
            • [Value point 2]

            Recommended next step: Schedule a follow-up with [decision maker] to discuss implementation timeline.

            Time is of the essence. When can we reconvene?

            Best,
            [Your Name]
            """,

        // Request feedback
        ["deal-message|Request feedback|professional"] = """
            Hi [First Name],

            I wanted to request feedback from the [Company Name] team on the proposal we submitted.

            Understanding your perspective will help us:
            • Address any concerns
            • Refine the solution as needed
            • Expedite the approval process

            Could you share any initial thoughts?

            Best regards,
            [Your Name]
            """,
        ["deal-message|Request feedback|friendly"] = """
            Hey [First Name]! 💭

            How's the team feeling about the proposal? Would love to hear any thoughts or questions!

            Even quick feedback helps us make sure we're on the right track for [Company Name].

            Let me know!

            [Your Name]
            """,
        ["deal-message|Request feedback|persuasive"] = """
            Hi [First Name],

            Your feedback on the proposal is critical to finalizing this deal.

            Specifically, I need to understand:
            • Is the pricing aligned with budget expectations?
            • Are there concerns from any stakeholders?
            • What would accelerate the approval?

            Direct feedback now prevents delays later. What can you share?

            Best,
            [Your Name]
            """,

        // Share resources
        ["deal-message|Share resources|professional"] = """
            Hi [First Name],

            I'm sharing some additional resources for the [Company Name] deal that may be helpful for internal discussions:

            • [Resource 1] — [Description]
            • [Resource 2] — [Description]
            • [Resource 3] — [Description]

            Please distribute these to relevant stakeholders. Let me know if you need anything else.

            Best regards,
            [Your Name]
            """,
        ["deal-message|Share resources|friendly"] = """
            Hey [First Name]! 📎

            Here are some resources that might help with the [Company Name] decision:

            • [Resource 1] — great for sharing with [stakeholder type]
            • [Resource 2] — answers a lot of common questions
            • [Resource 3] — the ROI stuff everyone loves

            Feel free to pass these around!

            [Your Name]
            """,
        ["deal-message|Share resources|persuasive"] = """
            Hi [First Name],

            I've prepared materials specifically designed to accelerate the [Company Name] decision:

            • [Resource 1] — builds the business case for [decision maker]
            • [Resource 2] — addresses technical concerns from [technical buyer]
            • [Resource 3] — competitive comparison your team requested

            These are your tools to drive consensus. When can we discuss how to deploy them?

            Best,
            [Your Name]
            """,

        // Check in on progress
        ["deal-message|Check in on progress|professional"] = """
            Hi [First Name],

            I wanted to check in on the status of the [Company Name] deal.

            Current understanding:
            • Stage: [current stage]
            • Next milestone: [milestone]
            • Target close: [date]

            Are we on track? Please let me know if there are any updates or if you need support.

            Best regards,
            [Your Name]
            """,
        ["deal-message|Check in on progress|friendly"] = """
            Hey [First Name]! 👋

            Quick check-in on the [Company Name] deal — how's everything going?

            Last I heard:
            • [Status point]
            • [Next step]

            Any updates? Here to help if needed!

            [Your Name]
            """,
        ["deal-message|Check in on progress|persuasive"] = """
            Hi [First Name],

            Deal check-in for [Company Name]:

            Current Status: [stage]
            Days in Stage: [number]
            Risk Level: [assessment]

            We're approaching [milestone/deadline]. What's needed to keep this moving?

            Let's address any blockers immediately.

            Best,
            [Your Name]
            """,

        // Close the deal
        ["deal-message|Close the deal|professional"] = """
            Hi [First Name],

            I wanted to provide a final update on the [Company Name] deal and confirm we're ready to close.

            Deal Summary:
            • Total Value: [amount]
            • Products/Services: [list]
            • Implementation Start: [date]

            The agreement is attached. Please let me know if you have any final questions or if we can proceed with signatures.

            Looking forward to a successful partnership.

            Best regards,
            [Your Name]
            """,
        ["deal-message|Close the deal|friendly"] = """
            Hey [First Name]! 🎉

            We're at the finish line with [Company Name]! Here's the summary:

            • Deal: [amount]
            • What's included: [products/services]
            • Kicking off: [date]

            Agreement is attached — let me know when you're ready to sign!

            So excited to get started!

            [Your Name]
            """,
        ["deal-message|Close the deal|persuasive"] = """
            Hi [First Name],

            It's time to finalize the [Company Name] partnership.

            DEAL TERMS:
            • Investment: [amount]
            • Deliverables: [products/services]
            • Value Timeline: ROI expected within [timeframe]

            The agreement is ready. Every day of delay pushes back the value [Company Name] will realize.

            I'm available now to address any final items. Let's close this today.

            Best,
            [Your Name]
            """,

        // ============================================
        // WORKFLOW MESSAGE TEMPLATES
        // ============================================

        // Schedule a meeting
        ["workflow-message|Schedule a meeting|professional"] = """
            Hi [First Name],

            I hope this message finds you well. I'm reaching out to schedule a brief conversation about how we might help [Company Name].

            Would you have 15 minutes this week or next to connect?

            Best regards,
            [Your Name]
            """,
        ["workflow-message|Schedule a meeting|friendly"] = """
            Hey [First Name]! 👋

            Hope you're having a great week! I'd love to find 15 minutes to chat about how we could help [Company Name].

            Any chance you're free this week?

            [Your Name]
            """,
        ["workflow-message|Schedule a meeting|persuasive"] = """
            Hi [First Name],

            I'll be brief: I have insights relevant to [Company Name]'s goals that I'd like to share.

            15 minutes is all I'm asking. Are you available this week?

            Best,
            [Your Name]
            """,

        // Follow up after demo
        ["workflow-message|Follow up after demo|professional"] = """
            Hi [First Name],

            Thank you for attending the demo. I wanted to follow up and see if you have any questions.

            I'm happy to provide additional information or schedule a follow-up conversation at your convenience.

            Best regards,
            [Your Name]
            """,
        ["workflow-message|Follow up after demo|friendly"] = """
            Hey [First Name]! 😊

            Thanks for joining the demo! Did you have any questions come up afterward?

            Happy to chat more anytime!

            [Your Name]
            """,
        ["workflow-message|Follow up after demo|persuasive"] = """
            Hi [First Name],

            Following the demo, I wanted to ensure you have everything needed to evaluate our solution.

            The teams we work with typically see results within [timeframe]. Ready to discuss how that applies to [Company Name]?

            Best,
            [Your Name]
            """,

        // Request feedback
        ["workflow-message|Request feedback|professional"] = """
            Hi [First Name],

            I wanted to follow up and see if you've had a chance to review the information I sent.

            Your feedback would be greatly appreciated. Please let me know your thoughts when you have a moment.

            Best regards,
            [Your Name]
            """,
        ["workflow-message|Request feedback|friendly"] = """
            Hey [First Name]! 👋

            Just checking in — did you get a chance to look at what I sent over?

            Would love to hear your thoughts!

            [Your Name]
            """,
        ["workflow-message|Request feedback|persuasive"] = """
            Hi [First Name],

            I'm following up on the materials I shared. Your feedback will help me understand if this is worth pursuing further.

            A simple reply would help me serve you better. What are your thoughts?

            Best,
            [Your Name]
            """,

        // Share resources
        ["workflow-message|Share resources|professional"] = """
            Hi [First Name],

            I thought you might find this resource valuable: [Resource Name]

            It covers [brief description] and may be relevant to what we discussed.

            Let me know if you'd like to discuss.

            Best regards,
            [Your Name]
            """,
        ["workflow-message|Share resources|friendly"] = """
            Hey [First Name]! 📚

            Found something you might like: [Resource Name]

            It's about [topic] — thought of you when I saw it!

            [Your Name]
            """,
        ["workflow-message|Share resources|persuasive"] = """
            Hi [First Name],

            I came across a resource directly relevant to the challenges we discussed: [Resource Name]

            Companies facing similar situations have found this invaluable. Worth a look.

            Best,
            [Your Name]
            """,

        // Check in on progress
        ["workflow-message|Check in on progress|professional"] = """
            Hi [First Name],

            I wanted to check in and see how things are progressing with [topic we discussed].

            If there's anything I can help with or if you'd like to schedule a call, please let me know.

            Best regards,
            [Your Name]
            """,
        ["workflow-message|Check in on progress|friendly"] = """
            Hey [First Name]! 👋

            Just wanted to check in! How's everything going with [topic]?

            Here if you need anything!

            [Your Name]
            """,
        ["workflow-message|Check in on progress|persuasive"] = """
            Hi [First Name],

            Checking in on [topic]. The window for [benefit/opportunity] is time-sensitive.

            Where do things stand on your end? I'd like to help if I can.

            Best,
            [Your Name]
            """,

        // Close the deal
        ["workflow-message|Close the deal|professional"] = """
            Hi [First Name],

            I wanted to follow up on our previous conversations. Have you had a chance to make a decision regarding [product/service]?

            I'm happy to answer any remaining questions or provide additional information to help with your decision.

            Best regards,
            [Your Name]
            """,
        ["workflow-message|Close the deal|friendly"] = """
            Hey [First Name]! 👋

            Just wanted to touch base — have you had a chance to think about moving forward?

            No pressure! Just let me know if you have any questions.

            [Your Name]
            """,
        ["workflow-message|Close the deal|persuasive"] = """
            Hi [First Name],

            We've had several great conversations, and I believe [product/service] is a strong fit for [Company Name].

            I'd like to help you move forward. What's the one thing standing between you and a decision?

            Best,
            [Your Name]
            """,
    };

    private const string DefaultCopy = """
        Hi [First Name],

        I hope this email finds you well. I wanted to reach out to see if you'd be interested in scheduling a brief call to discuss how our solutions can help [Company Name] achieve your goals.

        We've helped companies similar to yours:
        • Increase productivity by 40%
        • Reduce operational costs by 25%
        • Streamline workflows and save time

        Would you be available for a 15-minute call next week? I'd love to learn more about your current challenges and share how we can help.

        Looking forward to connecting.

        Best regards,
        [Your Name]
        """;

    public Task<string> GenerateAsync(
        string copyTypeId,
        string goal,
        string? context,
        string length,
        string? companyName,
        string? brandTone,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Normalize brand tone (default to professional)
        var tone = NormalizeTone(brandTone);

        // Try to find exact match with tone
        var key = $"{copyTypeId}|{goal}|{tone}";
        if (!Templates.TryGetValue(key, out var text))
        {
            // Fall back to professional tone
            key = $"{copyTypeId}|{goal}|professional";
            if (!Templates.TryGetValue(key, out text))
            {
                // Fall back to default
                text = DefaultCopy;
            }
        }

        // Replace company name
        if (!string.IsNullOrWhiteSpace(companyName))
        {
            text = text.Replace("[Company Name]", companyName, StringComparison.OrdinalIgnoreCase);
        }

        // Append context if provided
        if (!string.IsNullOrWhiteSpace(context))
        {
            var ctx = context.Trim();
            if (ctx.Length > 300) ctx = ctx[..300] + "...";
            text = text.TrimEnd() + $"\n\n---\nContext: {ctx}\n---";
        }

        // Adjust length
        text = AdjustLength(text, length);

        return Task.FromResult(text.Trim());
    }

    private static string NormalizeTone(string? tone) => tone?.ToLowerInvariant() switch
    {
        "friendly" => "friendly",
        "persuasive" => "persuasive",
        _ => "professional"
    };

    private static string AdjustLength(string text, string length)
    {
        if (string.Equals(length, "short", StringComparison.OrdinalIgnoreCase))
        {
            // Take first few paragraphs and add closing
            var paragraphs = text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
            if (paragraphs.Length > 3)
            {
                text = string.Join("\n\n", paragraphs.Take(3));
                if (!text.Contains("Best") && !text.Contains("[Your Name]"))
                {
                    text += "\n\nBest,\n[Your Name]";
                }
            }
        }
        else if (string.Equals(length, "long", StringComparison.OrdinalIgnoreCase))
        {
            // Add additional context for longer messages
            if (!text.Contains("case stud") && !text.Contains("technical"))
            {
                var insertion = "\n\nI'm also happy to share case studies or arrange a deeper technical discussion if that would be helpful.";
                // Insert before the closing
                var closingIndex = text.LastIndexOf("Best", StringComparison.OrdinalIgnoreCase);
                if (closingIndex > 0)
                {
                    text = text.Insert(closingIndex, insertion + "\n\n");
                }
                else
                {
                    text += insertion;
                }
            }
        }

        return text;
    }

    /// <summary>
    /// Rewrite copy with a different tone using template-based transformations.
    /// </summary>
    public Task<string> RewriteAsync(
        string originalCopy,
        string adjustment,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var result = adjustment?.ToLowerInvariant() switch
        {
            "shorter" or "short" => MakeShorter(originalCopy),
            "friendlier" or "friendly" => MakeFriendlier(originalCopy),
            "persuasive" or "more persuasive" => MakePersuasive(originalCopy),
            _ => originalCopy
        };

        return Task.FromResult(result);
    }

    /// <summary>
    /// Generate copy with recipient context for personalization.
    /// </summary>
    public Task<GenerateCopyResult> GenerateWithRecipientAsync(
        string copyTypeId,
        string goal,
        string? context,
        string length,
        string? companyName,
        string? brandTone,
        RecipientContext? recipient,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Generate base copy
        var body = GenerateAsync(copyTypeId, goal, context, length, companyName, brandTone, ct).Result;

        // Personalize with recipient data
        if (recipient != null)
        {
            if (!string.IsNullOrWhiteSpace(recipient.Name))
            {
                var firstName = recipient.Name.Split(' ')[0];
                body = body.Replace("[First Name]", firstName, StringComparison.OrdinalIgnoreCase);
                body = body.Replace("[Contact Name]", recipient.Name, StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrWhiteSpace(recipient.Company))
            {
                body = body.Replace("[Company Name]", recipient.Company, StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrWhiteSpace(recipient.Title))
            {
                body = body.Replace("[Title]", recipient.Title, StringComparison.OrdinalIgnoreCase);
            }
        }

        // Generate subject line for email types
        string? subject = null;
        if (copyTypeId.Contains("email", StringComparison.OrdinalIgnoreCase))
        {
            subject = GenerateSubjectLine(copyTypeId, goal, brandTone, recipient);
        }

        return Task.FromResult(new GenerateCopyResult(body, subject));
    }

    /// <summary>
    /// Generate copy in a specific language (template fallback just returns English with a note).
    /// </summary>
    public Task<GenerateCopyResult> GenerateInLanguageAsync(
        string copyTypeId,
        string goal,
        string? context,
        string length,
        string? companyName,
        string? brandTone,
        RecipientContext? recipient,
        string targetLanguage,
        CancellationToken ct = default)
    {
        // Template-based fallback cannot translate - return English with a note
        var result = GenerateWithRecipientAsync(copyTypeId, goal, context, length, companyName, brandTone, recipient, ct).Result;
        
        var languageName = GetLanguageName(targetLanguage);
        var note = $"\n\n[Note: Multi-language generation requires AI to be configured. Please translate this copy to {languageName}.]";
        
        return Task.FromResult(new GenerateCopyResult(result.Body + note, result.Subject));
    }

    private static string GetLanguageName(string languageCode) => languageCode?.ToLowerInvariant() switch
    {
        "es" or "spanish" => "Spanish",
        "fr" or "french" => "French",
        "de" or "german" => "German",
        "it" or "italian" => "Italian",
        "pt" or "portuguese" => "Portuguese",
        "nl" or "dutch" => "Dutch",
        "zh" or "chinese" => "Chinese (Simplified)",
        "ja" or "japanese" => "Japanese",
        "ko" or "korean" => "Korean",
        _ => "the target language"
    };

    private static string GenerateSubjectLine(string copyTypeId, string goal, string? brandTone, RecipientContext? recipient)
    {
        var companyName = recipient?.Company ?? "[Company]";
        var firstName = recipient?.Name?.Split(' ')[0] ?? "";

        var tone = NormalizeTone(brandTone);

        return (goal, tone) switch
        {
            ("Schedule a meeting", "friendly") => $"Quick chat, {firstName}? ☕",
            ("Schedule a meeting", "persuasive") => $"15 minutes that could change {companyName}'s trajectory",
            ("Schedule a meeting", _) => $"Meeting request: {companyName} partnership",

            ("Follow up after demo", "friendly") => $"Great chatting with you! 🎉",
            ("Follow up after demo", "persuasive") => $"Next steps for {companyName} — time sensitive",
            ("Follow up after demo", _) => $"Demo follow-up: Next steps for {companyName}",

            ("Request feedback", "friendly") => $"Quick question for you! 💭",
            ("Request feedback", "persuasive") => "Your input needed — quick response appreciated",
            ("Request feedback", _) => "Request for your feedback",

            ("Share resources", "friendly") => $"Thought you'd like this! 📚",
            ("Share resources", "persuasive") => $"Resources to accelerate {companyName}'s decision",
            ("Share resources", _) => "Resources for your review",

            ("Check in on progress", "friendly") => $"Just checking in! 👋",
            ("Check in on progress", "persuasive") => $"Following up — {companyName} opportunity",
            ("Check in on progress", _) => "Checking in on our conversation",

            ("Close the deal", "friendly") => $"Ready when you are! 🎯",
            ("Close the deal", "persuasive") => $"Final step: {companyName} agreement",
            ("Close the deal", _) => $"Agreement ready: {companyName} partnership",

            _ => "Following up on our conversation"
        };
    }

    private static string MakeShorter(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();
        var bulletCount = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Skip filler phrases
            if (trimmed.StartsWith("I hope this", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("I wanted to reach out", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("I'm also happy to share", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Limit bullets
            if (trimmed.StartsWith("•") || trimmed.StartsWith("-") || trimmed.StartsWith("*"))
            {
                if (bulletCount >= 2) continue;
                bulletCount++;
            }

            result.Add(line);
        }

        // Ensure we have a closing
        var final = string.Join("\n", result).Trim();
        if (!final.Contains("Best") && !final.Contains("[Your Name]") && !final.EndsWith("?"))
        {
            final += "\n\nBest,\n[Your Name]";
        }

        return final;
    }

    private static string MakeFriendlier(string text)
    {
        var result = text;

        // Add greeting emoji if starts with Hi/Hello/Hey
        if (result.StartsWith("Hi ", StringComparison.OrdinalIgnoreCase) ||
            result.StartsWith("Hello ", StringComparison.OrdinalIgnoreCase))
        {
            result = result.Replace("Hi ", "Hey ", StringComparison.OrdinalIgnoreCase);
            var firstLineEnd = result.IndexOf('\n');
            if (firstLineEnd > 0 && !result[..firstLineEnd].Contains("👋"))
            {
                result = result.Insert(firstLineEnd, " 👋");
            }
        }

        // Replace formal phrases
        result = result.Replace("I hope this email finds you well", "Hope you're having a great day", StringComparison.OrdinalIgnoreCase);
        result = result.Replace("I wanted to reach out", "Wanted to connect", StringComparison.OrdinalIgnoreCase);
        result = result.Replace("Best regards", "Cheers", StringComparison.OrdinalIgnoreCase);
        result = result.Replace("Looking forward to connecting", "Can't wait to chat", StringComparison.OrdinalIgnoreCase);
        result = result.Replace("Please let me know", "Just let me know", StringComparison.OrdinalIgnoreCase);
        result = result.Replace("Would you be available", "Any chance you're free", StringComparison.OrdinalIgnoreCase);

        // Add some emojis to bullet points
        result = result.Replace("• Increase productivity", "• 🚀 Boost productivity", StringComparison.OrdinalIgnoreCase);
        result = result.Replace("• Reduce operational costs", "• 💰 Cut costs", StringComparison.OrdinalIgnoreCase);
        result = result.Replace("• Streamline workflows", "• ⚡ Smoother workflows", StringComparison.OrdinalIgnoreCase);

        return result;
    }

    private static string MakePersuasive(string text)
    {
        var result = text;

        // Strengthen opening
        if (result.Contains("I wanted to reach out", StringComparison.OrdinalIgnoreCase))
        {
            result = result.Replace(
                "I wanted to reach out to see if you'd be interested in",
                "I'm reaching out because I believe you're missing out on",
                StringComparison.OrdinalIgnoreCase);
        }

        // Add urgency
        result = result.Replace("next week", "this week", StringComparison.OrdinalIgnoreCase);
        result = result.Replace("Would you be available", "Are you available", StringComparison.OrdinalIgnoreCase);

        // Strengthen value props
        result = result.Replace("by 40%", "by 40% within 90 days", StringComparison.OrdinalIgnoreCase);
        result = result.Replace("by 25%", "by 25% in the first quarter", StringComparison.OrdinalIgnoreCase);

        // Add urgency to closing
        if (result.Contains("Looking forward to connecting", StringComparison.OrdinalIgnoreCase))
        {
            result = result.Replace(
                "Looking forward to connecting",
                "The sooner we connect, the sooner you'll see results. I look forward to your response",
                StringComparison.OrdinalIgnoreCase);
        }

        // Add FOMO if space allows
        if (!result.Contains("competitors", StringComparison.OrdinalIgnoreCase) && result.Length < 1500)
        {
            var closingIndex = result.LastIndexOf("Best", StringComparison.OrdinalIgnoreCase);
            if (closingIndex > 0)
            {
                var urgencyLine = "\n\nYour competitors aren't waiting. Neither should you.\n\n";
                result = result.Insert(closingIndex, urgencyLine);
            }
        }

        return result;
    }
}
