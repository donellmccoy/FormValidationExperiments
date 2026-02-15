using Microsoft.EntityFrameworkCore;
using FormValidationExperiments.Shared.Enums;
using FormValidationExperiments.Shared.Models;

namespace FormValidationExperiments.Api.Data;

/// <summary>
/// Seeds the in-memory database with realistic sample LOD case data.
/// </summary>
public static class EctDbSeeder
{
    public static async Task SeedAsync(IDbContextFactory<EctDbContext> contextFactory)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        if (await context.Cases.AnyAsync())
            return;

        var members = GenerateMembers(20);
        context.Members.AddRange(members);
        await context.SaveChangesAsync();

        var cases = GenerateCases(100, members);
        context.Cases.AddRange(cases);
        await context.SaveChangesAsync();
    }

    private static List<Member> GenerateMembers(int count)
    {
        var rng = new Random(99);
        var members = new List<Member>(count);

        for (var i = 0; i < count; i++)
        {
            var (firstName, lastName) = Names[rng.Next(Names.Length)];
            var component = PickRandom(rng, ServiceComponent.RegularAirForce, ServiceComponent.AirForceReserve, ServiceComponent.AirNationalGuard, ServiceComponent.UnitedStatesSpaceForce);

            members.Add(new Member
            {
                FirstName = firstName,
                MiddleInitial = MiddleInitials[rng.Next(MiddleInitials.Length)],
                LastName = lastName,
                Rank = Ranks[rng.Next(Ranks.Length)],
                ServiceNumber = $"{rng.Next(100, 999)}-{rng.Next(10, 99)}-{rng.Next(1000, 9999)}",
                Unit = Units[rng.Next(Units.Length)],
                Component = component
            });
        }

        return members;
    }

    private static List<LineOfDutyCase> GenerateCases(int count, List<Member> members)
    {
        var rng = new Random(42); // Fixed seed for reproducibility
        var cases = new List<LineOfDutyCase>(count);

        for (var i = 0; i < count; i++)
        {
            var incidentDate = new DateTime(2024, 1, 1).AddDays(rng.Next(0, 540));
            var initiationDate = incidentDate.AddDays(rng.Next(1, 5));
            var processType = rng.Next(100) < 70 ? LineOfDutyProcessType.Informal : LineOfDutyProcessType.Formal;
            var component = PickRandom(rng, ServiceComponent.RegularAirForce, ServiceComponent.AirForceReserve, ServiceComponent.AirNationalGuard, ServiceComponent.UnitedStatesSpaceForce);
            var incidentType = PickRandom(rng, IncidentType.Injury, IncidentType.Illness, IncidentType.Disease, IncidentType.Death);
            var dutyStatus = PickRandom(rng, DutyStatus.Title10ActiveDuty, DutyStatus.InactiveDutyTraining, DutyStatus.Title32ActiveDuty, DutyStatus.NotInDutyStatus, DutyStatus.TravelToFromDuty);
            var finding = PickRandom(rng, LineOfDutyFinding.InLineOfDuty, LineOfDutyFinding.NotInLineOfDutyDueToMisconduct, LineOfDutyFinding.NotInLineOfDutyNotDueToMisconduct, LineOfDutyFinding.ExistingPriorToServiceNotAggravated);
            var wasUnderInfluence = rng.Next(100) < 15;
            var isInterim = processType == LineOfDutyProcessType.Informal && rng.Next(100) < 40;
            var completed = rng.Next(100) < 75;
            var rank = Ranks[rng.Next(Ranks.Length)];
            var (firstName, lastName) = Names[rng.Next(Names.Length)];
            var memberName = $"{firstName} {MiddleInitials[rng.Next(MiddleInitials.Length)]}. {lastName}";
            var unit = Units[rng.Next(Units.Length)];
            var (desc, diagnosis, findings) = GetIncidentData(rng, incidentType);

            var lodCase = new LineOfDutyCase
            {
                CaseId = $"{incidentDate:yyyyMMdd}-{(i + 1):D3}",
                MemberId = members[i % members.Count].Id,
                ProcessType = processType,
                Component = component,
                MemberName = memberName,
                MemberRank = rank,
                ServiceNumber = $"{rng.Next(100, 999)}-{rng.Next(10, 99)}-{rng.Next(1000, 9999)}",
                Unit = unit,
                IncidentType = incidentType,
                IncidentDate = incidentDate,
                IncidentDescription = desc,
                IncidentDutyStatus = dutyStatus,

                IsMilitaryFacility = rng.Next(100) < 60,
                TreatmentFacilityName = TreatmentFacilities[rng.Next(TreatmentFacilities.Length)],
                TreatmentDateTime = incidentDate.AddHours(rng.Next(1, 12)),
                ClinicalDiagnosis = diagnosis,
                MedicalFindings = findings,
                WasUnderInfluence = wasUnderInfluence,
                SubstanceType = wasUnderInfluence ? PickRandom(rng, SubstanceType.Alcohol, SubstanceType.Drugs, SubstanceType.Both) : null,
                WasMentallyResponsible = true,
                PsychiatricEvalCompleted = rng.Next(100) < 20,
                OtherRelevantConditions = rng.Next(100) < 30 ? "No significant prior medical history." : string.Empty,
                OtherTestsDone = rng.Next(100) < 50,
                IsServiceAggravated = null,
                IsPotentiallyUnfitting = rng.Next(100) < 35,
                IsAtDeployedLocation = rng.Next(100) < 10,
                RequiresArcBoard = component is ServiceComponent.AirForceReserve or ServiceComponent.AirNationalGuard && rng.Next(100) < 40,
                MedicalRecommendation = MedicalRecommendations[rng.Next(MedicalRecommendations.Length)],

                MemberStatementReviewed = true,
                MedicalRecordsReviewed = true,
                WitnessStatementsReviewed = rng.Next(100) < 70,
                PoliceReportsReviewed = wasUnderInfluence || rng.Next(100) < 20,
                CommanderReportReviewed = true,
                OtherSourcesReviewed = rng.Next(100) < 25,
                OtherSourcesDescription = string.Empty,
                MisconductExplanation = finding == LineOfDutyFinding.NotInLineOfDutyDueToMisconduct
                    ? MisconductExplanations[rng.Next(MisconductExplanations.Length)]
                    : string.Empty,

                InitiationDate = initiationDate,
                CompletionDate = completed ? initiationDate.AddDays(rng.Next(30, 150)) : null,
                TotalTimelineDays = processType == LineOfDutyProcessType.Informal ? 90 : 160,
                IsInterimLOD = isInterim,
                InterimLODExpiration = isInterim ? initiationDate.AddDays(90) : null,
                FinalFinding = finding,
                ProximateCause = finding != LineOfDutyFinding.InLineOfDuty
                    ? ProximateCauses[rng.Next(ProximateCauses.Length)]
                    : string.Empty,
                IsPriorServiceCondition = finding == LineOfDutyFinding.ExistingPriorToServiceNotAggravated,
                PSCDocumentation = string.Empty,
                EightYearRuleApplies = rng.Next(100) < 5,
                YearsOfService = rng.Next(1, 28),
                IsSexualAssaultCase = false,
                RestrictedReporting = false,
                SARCCoordination = string.Empty,
                ToxicologyReport = wasUnderInfluence ? $"BAC {(rng.Next(8, 25) / 100.0):F2}% at time of incident" : "Not applicable",
                MemberChoseMEDCON = rng.Next(100) < 40,
                IsAudited = rng.Next(100) < 15,
                PointOfContact = $"{unit.Split(',')[0].ToLower().Replace(" ", "")}.a1@us.af.mil",
                WitnessStatements = new List<string>(),
                AuditComments = new List<string>(),
                MEDCON = new MEDCONDetails
                {
                    IsEligible = rng.Next(100) < 50,
                    StartDate = initiationDate.AddDays(5),
                    EndDate = initiationDate.AddDays(95),
                    ExtensionDays = 0,
                    UsesInterimLOD = isInterim,
                    TreatmentPlan = string.Empty,
                    OutOfLocalAreaLeaveApproved = false,
                    PhysicianMemo = string.Empty
                },
                INCAP = new INCAPDetails
                {
                    IsEligible = component is ServiceComponent.AirForceReserve or ServiceComponent.AirNationalGuard && rng.Next(100) < 40,
                    CivilianIncomeLoss = rng.Next(100) < 30 ? rng.Next(1500, 8000) : 0m,
                    Documentation = string.Empty
                },
                Authorities = new List<LineOfDutyAuthority>
                {
                    new()
                    {
                        Role = "Immediate Commander",
                        Name = $"{CommanderNames[rng.Next(CommanderNames.Length)]}",
                        Rank = PickRandom(rng, "Lt Col", "Col", "Maj"),
                        Title = $"{unit.Split(',')[0]}/CC",
                        ActionDate = initiationDate.AddDays(rng.Next(5, 20)),
                        Recommendation = finding == LineOfDutyFinding.InLineOfDuty ? "Line of Duty" : "Not in Line of Duty",
                        Comments = new List<string> { "Reviewed all evidence and documentation." }
                    },
                    new()
                    {
                        Role = "Staff Judge Advocate",
                        Name = $"{SJANames[rng.Next(SJANames.Length)]}",
                        Rank = PickRandom(rng, "Maj", "Lt Col", "Capt"),
                        Title = $"{unit.Split(',')[0]}/JA",
                        ActionDate = initiationDate.AddDays(rng.Next(15, 30)),
                        Recommendation = "Legally sufficient",
                        Comments = new List<string> { "Package reviewed and found legally sufficient." }
                    }
                },
                Documents = new List<LineOfDutyDocument>
                {
                    new()
                    {
                        DocumentType = "AF Form 348",
                        FileName = $"AF348_{lastName}_{incidentDate:yyyyMMdd}-{(i + 1):D3}.pdf",
                        UploadDate = initiationDate.AddDays(rng.Next(3, 10)),
                        Description = "Line of Duty Determination form"
                    }
                },
                TimelineSteps = new List<TimelineStep>
                {
                    new()
                    {
                        StepDescription = "Member Reports Injury/Illness",
                        TimelineDays = rng.Next(1, 3),
                        StartDate = incidentDate,
                        CompletionDate = incidentDate.AddDays(rng.Next(0, 2)),
                        IsOptional = false
                    },
                    new()
                    {
                        StepDescription = "Medical Provider Review",
                        TimelineDays = rng.Next(3, 7),
                        StartDate = initiationDate,
                        CompletionDate = initiationDate.AddDays(rng.Next(3, 7)),
                        IsOptional = false
                    },
                    new()
                    {
                        StepDescription = "Commander Review and Endorsement",
                        TimelineDays = rng.Next(7, 21),
                        StartDate = initiationDate.AddDays(7),
                        CompletionDate = completed ? initiationDate.AddDays(rng.Next(14, 28)) : null,
                        IsOptional = false
                    }
                },
                Appeals = new List<LineOfDutyAppeal>()
            };

            cases.Add(lodCase);
        }

        return cases;
    }

    private static T PickRandom<T>(Random rng, params T[] values) => values[rng.Next(values.Length)];

    // ── Reference Data ──

    private static readonly (string First, string Last)[] Names =
    [
        ("Marcus", "Johnson"), ("Kyle", "Brennan"), ("Jessica", "Rodriguez"), ("David", "Chen"),
        ("Amanda", "Williams"), ("James", "Mitchell"), ("Sarah", "Park"), ("Robert", "Quinn"),
        ("Emily", "Thompson"), ("Michael", "Davis"), ("Rachel", "Garcia"), ("Christopher", "Lee"),
        ("Megan", "Torres"), ("Daniel", "Brown"), ("Samantha", "Wilson"), ("Andrew", "Martinez"),
        ("Nicole", "Anderson"), ("Brandon", "Taylor"), ("Ashley", "Thomas"), ("Joshua", "Jackson"),
        ("Brittany", "White"), ("Kevin", "Harris"), ("Stephanie", "Martin"), ("Ryan", "Robinson"),
        ("Lauren", "Clark"), ("Tyler", "Lewis"), ("Kayla", "Walker"), ("Justin", "Hall"),
        ("Amber", "Allen"), ("Patrick", "Young"), ("Tiffany", "King"), ("Jason", "Wright"),
        ("Christina", "Scott"), ("Nathan", "Green"), ("Monica", "Adams"), ("Brian", "Nelson"),
        ("Vanessa", "Hill"), ("Sean", "Campbell"), ("Heather", "Carter"), ("Derek", "Phillips"),
        ("Courtney", "Evans"), ("Travis", "Turner"), ("Lindsey", "Collins"), ("Adam", "Stewart"),
        ("Holly", "Sanchez"), ("Zachary", "Morris"), ("Erica", "Reed"), ("Dustin", "Cook"),
        ("Alicia", "Rogers"), ("Cody", "Morgan")
    ];

    private static readonly string[] MiddleInitials =
        ["A", "B", "C", "D", "E", "F", "G", "H", "J", "K", "L", "M", "N", "P", "R", "S", "T", "W"];

    private static readonly string[] Ranks =
        ["E-1", "E-2", "E-3", "E-4", "E-5", "E-6", "E-7", "E-8", "E-9", "O-1", "O-2", "O-3", "O-4", "O-5", "O-6"];

    private static readonly string[] Units =
    [
        "944th Fighter Wing, Luke AFB, AZ",
        "187th Attack Wing, Dannelly Field, AL",
        "1st Fighter Wing, JB Langley-Eustis, VA",
        "366th Fighter Wing, Mountain Home AFB, ID",
        "56th Fighter Wing, Luke AFB, AZ",
        "325th Fighter Wing, Tyndall AFB, FL",
        "4th Fighter Wing, Seymour Johnson AFB, NC",
        "388th Fighter Wing, Hill AFB, UT",
        "49th Wing, Holloman AFB, NM",
        "509th Bomb Wing, Whiteman AFB, MO",
        "2nd Bomb Wing, Barksdale AFB, LA",
        "341st Missile Wing, Malmstrom AFB, MT",
        "90th Missile Wing, FE Warren AFB, WY",
        "6th Air Refueling Wing, MacDill AFB, FL",
        "92nd Air Refueling Wing, Fairchild AFB, WA",
        "437th Airlift Wing, JB Charleston, SC",
        "60th Air Mobility Wing, Travis AFB, CA",
        "375th Air Mobility Wing, Scott AFB, IL",
        "19th Airlift Wing, Little Rock AFB, AR",
        "317th Airlift Wing, Dyess AFB, TX",
        "934th Airlift Wing, Minneapolis-St Paul ARS, MN",
        "452nd Air Mobility Wing, March ARB, CA",
        "514th Air Mobility Wing, JB McGuire-Dix-Lakehurst, NJ",
        "136th Airlift Wing, NAS JRB Fort Worth, TX",
        "167th Airlift Wing, Shepherd Field, WV"
    ];

    private static readonly string[] TreatmentFacilities =
    [
        "96th Medical Group, Eglin AFB",
        "59th Medical Wing, JBSA-Lackland",
        "David Grant USAF Medical Center, Travis AFB",
        "Keesler Medical Center, Keesler AFB",
        "Wright-Patterson Medical Center, WPAFB",
        "Womack Army Medical Center, Fort Liberty",
        "Regional Medical Center (civilian)",
        "VA Medical Center",
        "Base Medical Clinic",
        "Off-base Emergency Room"
    ];

    private static readonly string[] CommanderNames =
    [
        "James R. Mitchell", "Andrea Williams", "Robert E. Quinn", "Patricia Hernandez",
        "William Chang", "Maria Vasquez", "Thomas O'Brien", "Linda Kowalski",
        "Richard Nakamura", "Elizabeth Foster", "Charles Dubois", "Margaret Sullivan",
        "John Peterson", "Karen Blackwell", "George Ramirez", "Dorothy Nguyen"
    ];

    private static readonly string[] SJANames =
    [
        "Sarah Chen", "Michael Petrov", "Laura Graham", "David Okafor",
        "Jennifer Kim", "Mark Sorensen", "Allison Burke", "Steven Tanaka",
        "Catherine Moore", "Brian Weatherly", "Diana Cruz", "Paul Henriksen"
    ];

    private static readonly string[] MedicalRecommendations =
    [
        "Recommend follow-up in 2 weeks. Member placed on duty-limiting profile.",
        "Surgical consultation recommended. Member is non-deployable.",
        "Physical therapy 3x/week for rehabilitation. Follow-up in 30 days.",
        "No further treatment required. Member cleared for full duty.",
        "Referral to specialist. Temporary profile issued.",
        "Member requires orthopedic follow-up. Non-weight-bearing for 6 weeks.",
        "Occupational therapy and ergonomic assessment recommended.",
        "Follow-up imaging in 4 weeks. Activity restrictions in place.",
        "ADAPT referral recommended. Behavioral health follow-up scheduled.",
        "Conservative treatment with medication. Re-evaluate in 90 days."
    ];

    private static readonly string[] MisconductExplanations =
    [
        "Member was operating a privately owned vehicle while intoxicated in violation of UCMJ Article 111.",
        "Member engaged in reckless behavior contrary to orders and regulations.",
        "Member's voluntary actions were the proximate cause of the injury.",
        "Member failed to follow established safety procedures despite prior training.",
        "Member was engaged in prohibited recreational activity in violation of command policy."
    ];

    private static readonly string[] ProximateCauses =
    [
        "Member's own misconduct — driving under the influence of alcohol",
        "Member's willful negligence — failure to follow safety protocols",
        "Condition existed prior to service entry per medical records",
        "Member's voluntary engagement in prohibited activity",
        "Pre-existing condition not aggravated by military service"
    ];

    private static (string Description, string Diagnosis, string Findings) GetIncidentData(Random rng, IncidentType type)
    {
        return type switch
        {
            IncidentType.Injury => InjuryData[rng.Next(InjuryData.Length)],
            IncidentType.Illness => IllnessData[rng.Next(IllnessData.Length)],
            IncidentType.Disease => DiseaseData[rng.Next(DiseaseData.Length)],
            IncidentType.Death => DeathData[rng.Next(DeathData.Length)],
            _ => InjuryData[0]
        };
    }

    private static readonly (string Desc, string Diag, string Findings)[] InjuryData =
    [
        (
            "Member sustained right knee injury (ACL tear) during unit physical training session on base.",
            "Complete tear of the right anterior cruciate ligament (ACL), ICD-10: S83.511A",
            "MRI confirms complete ACL tear. Moderate joint effusion present. No fractures identified."
        ),
        (
            "Member fell from maintenance stand while performing aircraft servicing operations.",
            "Left distal radius fracture (Colles fracture), ICD-10: S52.531A",
            "X-ray confirms displaced fracture of the left distal radius. Closed reduction performed."
        ),
        (
            "Member involved in motor vehicle accident while traveling on official orders.",
            "Cervical strain, multiple contusions, ICD-10: S13.4XXA, T14.8",
            "CT of c-spine negative for fracture. Soft tissue swelling noted. Neurologically intact."
        ),
        (
            "Member strained lower back while lifting heavy equipment during deployment exercise.",
            "Lumbar strain with radiculopathy, ICD-10: M54.41",
            "MRI shows L4-L5 disc herniation with nerve root compression."
        ),
        (
            "Member sustained concussion during combatives training.",
            "Mild traumatic brain injury (concussion), ICD-10: S06.0X0A",
            "CT head negative for bleed. Post-concussion symptoms present. Neurology consult ordered."
        ),
        (
            "Member fractured right ankle stepping in a drainage ditch during night operations.",
            "Right lateral malleolus fracture, ICD-10: S82.61XA",
            "X-ray confirms non-displaced fracture. Cast applied. Weight-bearing as tolerated in 6 weeks."
        ),
        (
            "Member suffered second-degree burns during hot engine maintenance.",
            "Second-degree thermal burns, right forearm, ICD-10: T22.111A",
            "Burns covering approximately 5% TBSA on right forearm. Wound care initiated."
        ),
        (
            "Member dislocated left shoulder during PT obstacle course.",
            "Anterior dislocation of left glenohumeral joint, ICD-10: S43.004A",
            "Reduction performed in ER. Post-reduction X-ray confirms anatomic alignment."
        ),
        (
            "Member lacerated right hand on sheet metal during aircraft maintenance.",
            "Complex laceration, right hand, with extensor tendon involvement, ICD-10: S61.411A",
            "Surgical repair of extensor tendon performed. No neurovascular compromise."
        ),
        (
            "Member sustained bilateral wrist injuries from a fall during icy conditions on the flightline.",
            "Bilateral wrist sprains, ICD-10: S63.501A",
            "X-rays negative for fracture bilaterally. Splints applied. Follow-up in 2 weeks."
        )
    ];

    private static readonly (string Desc, string Diag, string Findings)[] IllnessData =
    [
        (
            "Member developed severe respiratory symptoms during overseas deployment.",
            "Community-acquired pneumonia, ICD-10: J18.9",
            "Chest X-ray shows right lower lobe infiltrate. Blood cultures pending."
        ),
        (
            "Member presented with acute abdominal pain requiring emergency surgery.",
            "Acute appendicitis with perforation, ICD-10: K35.20",
            "CT abdomen confirms perforated appendicitis. Emergency appendectomy performed."
        ),
        (
            "Member experienced cardiac symptoms during physical fitness assessment.",
            "Supraventricular tachycardia (SVT), ICD-10: I47.1",
            "EKG shows SVT with rate of 180 bpm. Adenosine administered with conversion to sinus rhythm."
        ),
        (
            "Member developed heat-related illness during summer field exercise.",
            "Exertional heat stroke, ICD-10: T67.01XA",
            "Core temp 104.2°F on arrival. Rapid cooling initiated. Labs show rhabdomyolysis."
        ),
        (
            "Member reported persistent headaches and vision changes over 3-week period.",
            "Migraine with aura, ICD-10: G43.109",
            "Neurological exam and MRI brain within normal limits. Consistent with complex migraine."
        )
    ];

    private static readonly (string Desc, string Diag, string Findings)[] DiseaseData =
    [
        (
            "Member diagnosed with autoimmune condition affecting joint mobility.",
            "Rheumatoid arthritis, ICD-10: M06.9",
            "Lab work positive for RF and anti-CCP antibodies. Joint imaging shows early erosive changes."
        ),
        (
            "Member found to have elevated blood sugar during annual PHA.",
            "Type 2 diabetes mellitus, ICD-10: E11.9",
            "HbA1c 8.2%. Fasting glucose 210 mg/dL. Endocrinology referral initiated."
        ),
        (
            "Member diagnosed with chronic respiratory condition after occupational exposure.",
            "Occupational asthma, ICD-10: J45.20",
            "PFTs show obstructive pattern. Methacholine challenge positive. Linked to JP-8 exposure."
        ),
        (
            "Member developed skin condition requiring dermatology evaluation.",
            "Psoriasis vulgaris, ICD-10: L40.0",
            "Widespread plaque psoriasis affecting >10% BSA. Systemic therapy recommended."
        ),
        (
            "Member diagnosed with hypertension during routine screening.",
            "Essential hypertension, ICD-10: I10",
            "Ambulatory BP monitoring confirms sustained hypertension. Average 152/96 mmHg."
        )
    ];

    private static readonly (string Desc, string Diag, string Findings)[] DeathData =
    [
        (
            "Member found unresponsive in dormitory room. Resuscitation unsuccessful.",
            "Sudden cardiac death, ICD-10: I46.1",
            "Autopsy reveals hypertrophic cardiomyopathy as cause of death."
        ),
        (
            "Member fatally injured in training accident involving heavy equipment.",
            "Traumatic injuries incompatible with life, ICD-10: T14.91",
            "Multiple blunt force trauma injuries. Death pronounced at scene by medical personnel."
        ),
        (
            "Member died from complications following deployment-related illness.",
            "Sepsis secondary to pneumonia, ICD-10: A41.9",
            "Progressive multi-organ failure despite ICU management. Cultures grew S. pneumoniae."
        )
    ];
}
