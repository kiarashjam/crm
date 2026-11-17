namespace ACI.Application.Common;

/// <summary>The languages a prepared email can be written in.</summary>
public static class EmailTemplateLanguages
{
    public const string French = "fr";
    public const string English = "en";

    /// <summary>In the order the composer offers them.</summary>
    public static readonly IReadOnlyList<string> Supported = [French, English];

    public static bool IsSupported(string? language) =>
        language != null && Supported.Contains(language.Trim().ToLowerInvariant());
}

/// <summary>One language of a starter prepared email.</summary>
public sealed record StarterEmailVersion(string Language, string Subject, string Body);

/// <summary>A prepared email every workspace is given to start from, and may change or delete.</summary>
/// <param name="Key">Which one it is, kept with the workspace's copy so the set is given once.</param>
/// <param name="ClubOnly">Only for a workspace with the club on.</param>
public sealed record StarterEmailTemplate(string Key, string Name, string Category, string Description, bool ClubOnly, IReadOnlyList<StarterEmailVersion> Versions);

/// <summary>
/// The prepared emails a workspace starts with, in French and English. They use the
/// placeholders the outbox fills for each recipient, and [[blanks]] that the sender fills
/// in before sending: the composer will not send one while any is left.
/// </summary>
public static class StarterEmailTemplates
{
    private const string SignEn = "{{senderName}}\n{{workspace}}";
    private const string SignFr = "{{senderName}}\n{{workspace}}";
    private const string DearEn = "Dear {{firstName|Sir or Madam}},";
    private const string DearFr = "Bonjour {{firstName|Madame, Monsieur}},";

    public static readonly IReadOnlyList<StarterEmailTemplate> All =
    [
        new("thanks-for-interest", "Thank you for your interest", "First contact",
            "After someone gets in touch for the first time.", false,
        [
            new(EmailTemplateLanguages.French, "Merci de votre intérêt pour {{workspace}}",
                $"{DearFr}\n\nMerci de nous avoir contactés et de l'intérêt que vous portez à {{{{workspace}}}}. C'est un plaisir d'avoir de vos nouvelles.\n\nJe me réjouis de vous en dire davantage et de répondre à toutes vos questions. Auriez-vous quelques minutes pour un appel ces prochains jours ? Il vous suffit de répondre à cet e-mail en m'indiquant le moment qui vous convient.\n\nAvec mes meilleures salutations,\n{SignFr}"),
            new(EmailTemplateLanguages.English, "Thank you for your interest in {{workspace}}",
                $"{DearEn}\n\nThank you for getting in touch and for your interest in {{{{workspace}}}}. It was a pleasure to hear from you.\n\nI would be delighted to tell you more and to answer any questions you may have. Would you have a few minutes for a call in the coming days? Simply reply to this email with a time that suits you.\n\nWith kind regards,\n{SignEn}"),
        ]),
        new("follow-up", "Follow-up after our conversation", "Follow-up",
            "After a call or a meeting, to recap and agree the next step.", false,
        [
            new(EmailTemplateLanguages.French, "Suite à notre échange",
                $"{DearFr}\n\nMerci pour le temps que vous m'avez accordé [[aujourd'hui / le jour]]. J'ai beaucoup apprécié notre échange.\n\nComme convenu, voici un bref résumé :\n- [[ce dont nous avons parlé]]\n- [[la prochaine étape convenue]]\n\nN'hésitez pas à revenir vers moi pour toute question d'ici là.\n\nAvec mes meilleures salutations,\n{SignFr}"),
            new(EmailTemplateLanguages.English, "Following our conversation",
                $"{DearEn}\n\nThank you for your time [[today / on the day]]. I very much enjoyed our conversation.\n\nAs agreed, here is a short summary:\n- [[what we discussed]]\n- [[the next step we agreed]]\n\nPlease don't hesitate to come back to me with any question in the meantime.\n\nWith kind regards,\n{SignEn}"),
        ]),
        new("meeting-confirmation", "Meeting confirmation", "Meetings",
            "Confirms a meeting's date, time and place.", false,
        [
            new(EmailTemplateLanguages.French, "Confirmation de notre rendez-vous du [[date]]",
                $"{DearFr}\n\nJ'ai le plaisir de vous confirmer notre rendez-vous :\n\nDate : [[date]]\nHeure : [[heure]]\nLieu : [[adresse ou lien de visioconférence]]\n\nSi un empêchement survenait de votre côté, il vous suffit de répondre à cet e-mail et nous trouverons un autre moment.\n\nAu plaisir de vous rencontrer.\n\nAvec mes meilleures salutations,\n{SignFr}"),
            new(EmailTemplateLanguages.English, "Confirming our meeting on [[date]]",
                $"{DearEn}\n\nI am pleased to confirm our meeting:\n\nDate: [[date]]\nTime: [[time]]\nPlace: [[address or video link]]\n\nIf anything changes on your side, simply reply to this email and we will find another time.\n\nI look forward to meeting you.\n\nWith kind regards,\n{SignEn}"),
        ]),
        new("documents", "Sending the documents", "Follow-up",
            "Sends documents or information that were asked for.", false,
        [
            new(EmailTemplateLanguages.French, "Les documents demandés",
                $"{DearFr}\n\nComme promis, je vous fais parvenir [[les documents demandés]] [[en pièce jointe / à ce lien : …]].\n\nSi quelque chose n'est pas clair ou si vous avez besoin d'autre chose, je reste volontiers à votre disposition.\n\nAvec mes meilleures salutations,\n{SignFr}"),
            new(EmailTemplateLanguages.English, "The documents you asked for",
                $"{DearEn}\n\nAs promised, I am sending you [[the documents you asked for]] [[attached / at this link: …]].\n\nShould anything be unclear, or if you need anything else, I will gladly help.\n\nWith kind regards,\n{SignEn}"),
        ]),
        new("reminder", "Gentle reminder", "Follow-up",
            "A friendly nudge when you have not heard back.", false,
        [
            new(EmailTemplateLanguages.French, "Petit rappel",
                $"{DearFr}\n\nJe me permets de revenir vers vous au sujet de [[mon précédent message / notre proposition]], au cas où cela vous aurait échappé.\n\nAuriez-vous un instant pour me dire où vous en êtes ? Je reste à votre disposition pour toute question.\n\nAvec mes meilleures salutations,\n{SignFr}"),
            new(EmailTemplateLanguages.English, "A gentle reminder",
                $"{DearEn}\n\nI am coming back to you about [[my previous message / our proposal]], in case it slipped through.\n\nWould you have a moment to let me know where things stand? I remain at your disposal for any question.\n\nWith kind regards,\n{SignEn}"),
        ]),
        new("thank-you", "Thank you", "Follow-up",
            "Thanks someone after a decision, a signature or a visit.", false,
        [
            new(EmailTemplateLanguages.French, "Merci de la part de {{workspace}}",
                $"{DearFr}\n\nUn grand merci pour [[votre confiance / votre visite / votre décision]]. Cela nous touche beaucoup.\n\n[[Un mot sur la suite.]]\n\nNous sommes ravis de vous compter parmi nous et restons à votre entière disposition.\n\nAvec nos chaleureuses salutations,\n{SignFr}"),
            new(EmailTemplateLanguages.English, "Thank you from {{workspace}}",
                $"{DearEn}\n\nThank you very much for [[your trust / your visit / your decision]]. It means a great deal to us.\n\n[[A word on what happens next.]]\n\nWe are delighted to count you among us and remain at your disposal for anything you may need.\n\nWith warm regards,\n{SignEn}"),
        ]),
        new("club-visit-invitation", "Invitation to visit the club", "Club",
            "Invites a prospective member to come and see the club.", true,
        [
            new(EmailTemplateLanguages.French, "Une invitation à découvrir {{workspace}}",
                $"{DearFr}\n\nNous aurions le plaisir de vous accueillir à {{{{workspace}}}} pour une visite privée, afin de vous faire découvrir le club et ses espaces, et de vous en dire davantage sur l'adhésion.\n\n[[Une ou deux dates]] vous conviendraient-elles ? La visite dure environ [[une heure]], et c'est avec plaisir que nous accueillerons également [[votre conjoint / un invité]].\n\nIl vous suffit de répondre à cet e-mail et je m'occupe de tout organiser.\n\nAvec nos chaleureuses salutations,\n{SignFr}"),
            new(EmailTemplateLanguages.English, "An invitation to discover {{workspace}}",
                $"{DearEn}\n\nIt would be our pleasure to welcome you to {{{{workspace}}}} for a private visit, to show you the club and its spaces and to tell you more about membership.\n\nWould [[a date or two]] suit you? The visit takes about [[an hour]], and you are most welcome to bring [[your partner / a guest]].\n\nSimply reply to this email and I will arrange everything.\n\nWith warm regards,\n{SignEn}"),
        ]),
        new("club-after-visit", "After the visit", "Club",
            "Thanks them for visiting and opens the way to membership.", true,
        [
            new(EmailTemplateLanguages.French, "Merci de votre visite à {{workspace}}",
                $"{DearFr}\n\nMerci de votre visite à {{{{workspace}}}} [[aujourd'hui / le jour]]. Ce fut un réel plaisir de vous faire découvrir les lieux.\n\nSi vous souhaitez nous rejoindre, je vous accompagnerai volontiers dans les prochaines étapes : [[comment déposer sa candidature]].\n\nN'hésitez pas à me contacter pour toute question.\n\nAvec nos chaleureuses salutations,\n{SignFr}"),
            new(EmailTemplateLanguages.English, "Thank you for visiting {{workspace}}",
                $"{DearEn}\n\nThank you for visiting {{{{workspace}}}} [[today / on the day]]. It was a real pleasure to show you around.\n\nShould you wish to join us, I would be glad to guide you through the next steps: [[how to apply]].\n\nPlease don't hesitate to get in touch with any question.\n\nWith warm regards,\n{SignEn}"),
        ]),
        new("club-application-received", "Application received", "Club",
            "Confirms that a membership application has arrived and says what happens next.", true,
        [
            new(EmailTemplateLanguages.French, "Nous avons bien reçu votre candidature",
                $"{DearFr}\n\nMerci pour votre candidature à {{{{workspace}}}}. Nous l'avons bien reçue et l'examinerons avec la plus grande attention.\n\nVous aurez de nos nouvelles d'ici au [[date]]. D'ici là, je reste à votre disposition pour toute question.\n\nAvec nos chaleureuses salutations,\n{SignFr}"),
            new(EmailTemplateLanguages.English, "We have received your application",
                $"{DearEn}\n\nThank you for your application to join {{{{workspace}}}}. We have received it and will consider it with the greatest care.\n\nYou will hear from us by [[date]]. In the meantime, I remain at your disposal for any question.\n\nWith warm regards,\n{SignEn}"),
        ]),
        new("club-event-invitation", "Event invitation", "Club",
            "Invites someone to an event at the club.", true,
        [
            new(EmailTemplateLanguages.French, "Invitation : [[événement]]",
                $"{DearFr}\n\nNous avons le plaisir de vous convier à [[l'événement]] à {{{{workspace}}}}.\n\nDate : [[date]]\nHeure : [[heure]]\n[[Un mot sur la soirée.]]\n\nNous vous serions reconnaissants de nous confirmer votre présence d'ici au [[date de réponse]], simplement en répondant à cet e-mail.\n\nAu plaisir de vous accueillir.\n\nAvec nos chaleureuses salutations,\n{SignFr}"),
            new(EmailTemplateLanguages.English, "You are invited: [[event]]",
                $"{DearEn}\n\nWe are delighted to invite you to [[the event]] at {{{{workspace}}}}.\n\nDate: [[date]]\nTime: [[time]]\n[[A word about the evening.]]\n\nWe would be grateful if you could let us know whether you will join us by [[reply date]], simply by replying to this email.\n\nWe look forward to welcoming you.\n\nWith warm regards,\n{SignEn}"),
        ]),
    ];
}
