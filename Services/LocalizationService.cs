namespace Darge.Web.Services;

public interface ILocalizationService
{
    string Get(string key, string? culture = null);
}

public class LocalizationService : ILocalizationService
{
    private static readonly Dictionary<string, (string En, string Ka)> Translations = new()
    {
        { "BrandName", ("DARGE", "DARGE") },
        { "BrandTagline", ("AgriTech & AI Platform", "აგრო-ტექ და AI პლატფორმა") },

        // DB System Values & Achievements (Backwards Compatible)
        { "First Project", ("First Project", "პირველი პროექტი") },
        { "FIRST_PROJECT", ("First Project", "პირველი პროექტი") },
        { "Getting Started", ("Getting Started", "ნაბიჯი პირველი") },
        { "GETTING_STARTED", ("Getting Started", "ნაბიჯი პირველი") },
        { "100 XP", ("100 XP Milestone", "100 XP მიღწევა") },
        { "XP_100", ("100 XP Milestone", "100 XP მიღწევა") },
        { "Beginner", ("Beginner", "დამწყები") },
        { "Intermediate", ("Intermediate", "საშუალო") },
        { "Advanced", ("Advanced", "გამოცდილი") },
        { "Sunny Balcony", ("Sunny Balcony", "მზიანი აივანი") },
        { "Backyard Garden", ("Backyard Garden", "ეზოს ბოსტანი") },
        { "Greenhouse", ("Greenhouse", "სათბური") },
        { "Indoor / Windowsill", ("Indoor / Windowsill", "ოთახის რაფა") },
        { "Raised Bed", ("Raised Bed", "ამოწეული კვლევი") },

        // Sidebar & Auth Navigation
        { "NavDashboard", ("Dashboard", "მართვის პანელი") },
        { "NavMyProjects", ("My Projects", "ჩემი პროექტები") },
        { "NavPlanWithAI", ("Plan with AI", "გეგმა AI-ით") },
        { "NavDargeAI", ("DARGE AI", "DARGE AI") },
        { "NavProfile", ("Profile", "პროფილი") },
        { "NavSignOut", ("Sign Out", "გამოსვლა") },
        { "NavSignIn", ("Sign In", "შესვლა") },
        { "NavGetStarted", ("Get Started", "დაწყება") },
        { "NavRegister", ("Register", "რეგისტრაცია") },

        // Dashboard
        { "WelcomeBack", ("Welcome back", "მოგესალმებით") },
        { "DashboardSub", ("Here's your garden status, upcoming tasks, and AI insights.", "აი თქვენი ბაღის სტატუსი, მიმდინარე დავალებები და AI რჩევები.") },
        { "TotalXP", ("Total Experience", "ჯამური გამოცდილება") },
        { "ActiveProjects", ("Active Projects", "აქტიური პროექტები") },
        { "TaskCompletion", ("Task Completion", "დავალებების შესრულება") },
        { "UnlockedBadges", ("Unlocked Badges", "მიღწეული ბეჯები") },
        { "ActionItems", ("Action Items & Today's Tasks", "დღევანდელი დავალებები") },
        { "PendingActions", ("Pending Actions", "შესასრულებელი დავალებები") },
        { "MarkComplete", ("Mark Complete", "შესრულება") },
        { "ViewAllProjects", ("View All Projects", "ყველა პროექტი") },
        { "AIInsightTitle", ("DARGE AI Assistant", "DARGE AI ასისტენტი") },
        { "AIInsightDesc", ("Get instant, personalized advice on watering, pest control, soil care, or garden timing.", "მიიღეთ პერსონალიზებული რჩევები მორწყვაზე, მავნებლებზე და მოვლაზე.") },
        { "AskAIWhatNeedsAttention", ("Ask DARGE AI What Needs Attention", "კითხე DARGE AI-ს რა მოითხოვს ყურადღებას") },
        { "LevelGardener", ("Level Gardener", "დონის მებაღე") },
        { "AchievementsLevel", ("Achievements & Level", "მიღწევები და დონე") },
        { "RecentMilestones", ("Recent Milestones", "ბოლო მიღწევები") },
        { "ViewFullProfile", ("View Full Profile & Achievements", "სრული პროფილის ნახვა") },
        { "AllCaughtUp", ("All caught up!", "ყველა დავალება შესრულებულია!") },
        { "NoPendingTasks", ("No pending tasks across your projects. You can add new tasks or generate a new AI project plan.", "პროექტებში შესასრულებელი დავალებები არ არის.") },
        { "NoActiveProjects", ("No active projects", "აქტიური პროექტები არ არის") },
        { "StartGardenJourney", ("Start your garden journey by creating a manual project or AI-generated plan.", "დაიწყეთ პროექტის შექმნით ან AI-ით დაგეგმვით.") },
        { "NextAction", ("Next Action:", "შემდეგი ნაბიჯი:") },
        { "AllTasksComplete", ("✓ All tasks complete", "✓ ყველა დავალება შესრულებულია") },
        { "OpenProjectDetails", ("Open Project Details", "პროექტის დეტალები") },
        { "XPToNextLevel", ("XP to Level", "XP შემდეგ დონემდე") },
        { "NoAchievementsUnlocked", ("No achievements unlocked yet. Keep completing project tasks to earn XP!", "მიღწევები ჯერ არ არის გახსნილი. შეასრულეთ დავალებები XP-ის მისაღებად!") },
        { "Unlocked", ("Unlocked", "მიღწეულია") },
        { "UnlockedMilestone", ("Unlocked Milestone", "გახსნილი მიღწევა") },

        // Projects Workspace & Details
        { "ProjectsWorkspaceTitle", ("Projects Workspace", "პროექტების სამუშაო სივრცე") },
        { "ProjectsWorkspaceSub", ("Manage your growing crops, garden beds, and AI-generated plans.", "მართეთ თქვენი მცენარეები, ბოსტნები და AI გეგმები.") },
        { "NewProject", ("New Project", "ახალი პროექტი") },
        { "OpenDetails", ("Open Details", "დეტალების ნახვა") },
        { "AIAssistantBtn", ("AI Assistant", "AI ასისტენტი") },
        { "Location", ("Location", "მდებარეობა") },
        { "SoilType", ("Soil Type", "ნიადაგის ტიპი") },
        { "Space", ("Allocated Space", "ფართობი") },
        { "TasksDone", ("Tasks Done", "შესრულებული დავალებები") },
        { "TasksToDo", ("Tasks To Do", "შესასრულებელი დავალებები") },
        { "CompletedTasks", ("Completed Tasks", "შესრულებული დავალებები") },
        { "OpenAIAssistant", ("Open AI Assistant", "AI ასისტენტის გახსნა") },
        { "DeleteProject", ("Delete Project", "პროექტის წაშლა") },
        { "BackToProjects", ("Back to Projects Workspace", "პროექტების სამუშაო სივრცეში დაბრუნება") },
        { "AddNewTask", ("Add New Task", "ახალი დავალების დამატება") },
        { "TaskName", ("Task Name", "დავალების დასახელება") },
        { "Description", ("Description", "აღწერა") },
        { "DifficultyLevel", ("Difficulty Level", "სირთულის დონე") },
        { "Easy", ("Easy", "იოლი") },
        { "Medium", ("Medium", "საშუალო") },
        { "Hard", ("Hard", "რთული") },
        { "Delete", ("Delete", "წაშლა") },
        { "Complete", ("Complete", "შესრულება") },
        { "NoProjectsFound", ("No projects found", "პროექტები ვერ მოიძებნა") },
        { "StartTracking", ("Start tracking your plants and crops. You can create a manual project or let DARGE AI design a tailored plan.", "დაიწყეთ თქვენი მცენარეების აღრიცხვა.") },
        { "CreateManualProject", ("Create Manual Project", "მექანიკური პროექტის შექმნა") },

        // Plan with AI
        { "PlanWithAITitle", ("Plan with DARGE AI", "დაგეგმე DARGE AI-ით") },
        { "PlanWithAISub", ("Describe your environment and DARGE AI will build a complete project plan.", "აღწერეთ თქვენი გარემო და DARGE AI შექმნის სრულყოფილ გეგმას.") },
        { "PlantNameLabel", ("Plant / Crop Name", "მცენარის დასახელება") },
        { "LocationLabel", ("Location / City / Region", "მდებარეობა / ქალაქი / რეგიონი") },
        { "EnvironmentLabel", ("Growing Environment", "ზრდის გარემო") },
        { "SpaceLabel", ("Available Space (m²)", "ხელმისაწვდომი ფართობი (მ²)") },
        { "ExperienceLabel", ("Experience Level", "გამოცდილების დონე") },
        { "GoalLabel", ("Your Goal / Preferences", "თქვენი მიზანი / უპირატესობები") },
        { "GeneratePlanBtn", ("Generate Project Plan", "პროექტის გეგმის გენერაცია") },
        { "CreateThisProjectBtn", ("Create This Project", "ამ პროექტის შექმნა") },
        { "ReviewPlanTitle", ("Review AI Plan", "AI გეგმის განხილვა") },
        { "RegeneratePlan", ("Regenerate Plan", "ხელახალი გენერაცია") },
        { "Summary", ("Summary", "მოკლე მიმოხილვა") },
        { "Recommendations", ("Key Recommendations", "მთავარი რეკომენდაციები") },
        { "ProjectAttributes", ("Project Attributes", "პროექტის პარამეტრები") },
        { "GeneratedTasks", ("Generated Tasks", "გენერირებული დავალებები") },
        { "ReadyToStart", ("Ready to start growing?", "მზად ხართ გასაშენებლად?") },
        { "ReadySub", ("Clicking below will save this project and insert these AI-generated tasks into your garden workspace.", "ქვემოთ დაჭერით შეინახავთ პროექტს და დავალებებს.") },

        // Global & Project AI
        { "GlobalAITitle", ("DARGE AI Workspace", "DARGE AI სამუშაო სივრცე") },
        { "GlobalAISub", ("Ask general gardening questions or select one of your projects for contextual advice.", "დასვით ზოგადი კითხვები ან აირჩიეთ პროექტი კონტექსტური რჩევებისთვის.") },
        { "GeneralMode", ("General Gardening Mode (All Crops)", "ზოგადი მებაღეობის რეჟიმი") },
        { "AskAIBtn", ("Ask AI", "კითხე AI-ს") },
        { "AskPlaceholder", ("Ask DARGE AI any gardening question...", "დასვით ნებისმიერი კითხვა მებაღეობაზე...") },
        { "SuggestedQuestions", ("Suggested Questions:", "შემოთავაზებული კითხვები:") },
        { "BalconyVeg", ("Balcony Vegetables?", "აივნის ბოსტნეული?") },
        { "SoilPrep", ("Soil Preparation?", "ნიადაგის მომზადება?") },
        { "PestControl", ("Natural Pest Control?", "მავნებლებისგან დაცვა?") },
        { "FertilizerTips", ("Fertilization Tips?", "სასუქის რჩევები?") },
        { "PoweredByGemini", ("Powered by Gemini AI", "მუშაობს Gemini AI-ით") },
        { "DedicatedAI", ("Dedicated Project AI", "პროექტის AI ასისტენტი") },
        { "StarterPrompts", ("Starter prompts:", "საწყისი კითხვები:") },
        { "WhatToDoThisWeek", ("What should I do this week?", "რა გავაკეთო ამ კვირაში?") },
        { "IsOnSchedule", ("Is my project on schedule?", "გრაფიკში ვარ?") },
        { "WhatToPrepareNext", ("What should I prepare next?", "რა მოვამზადო შემდეგში?") },
        { "ExplainNextTask", ("Explain my next task", "აგიხსნათ შემდეგი დავალება") },
        { "CheckStressSigns", ("Check stress signs", "სტრესის ნიშნების შემოწმება") },
        { "AiQuotaExceeded", ("DARGE AI has reached its temporary usage limit. Please try again later.", "DARGE AI-მ დროებით მოხმარების ლიმიტს მიაღწია. გთხოვთ, სცადოთ მოგვიანებით.") },

        // Landing Page Marketing
        { "LandingHeroTagline", ("AI-Powered AgriTech & Smart Gardening", "AI-ით მართული აგრო-ტექ და ჭკვიანი მებაღეობა") },
        { "LandingHeroTitle", ("Plan, Grow, and Scale Your Crops with AI", "დაგეგმეთ, გააშენეთ და მართეთ მოსავალი AI-ით") },
        { "LandingHeroDesc", ("DARGE gives urban farmers and hobby growers structured project plans, task milestones, XP rewards, and real-time AI guidance in English and Georgian.", "DARGE აძლევს მებაღეებსა და ფერმერებს სტრუქტურირებულ გეგმებს, დავალებებს, XP ჯილდოებსა და რეალური დროის AI რჩევებს.") },
        { "HeroFeature1", ("✓ AI Climate & Soil Plans", "✓ AI კლიმატისა და ნიადაგის გეგმები") },
        { "HeroFeature2", ("✓ 🇬🇪 Georgian & English AI", "✓ 🇬🇪 ქართული და ინგლისური AI") },
        { "HeroFeature3", ("✓ Task & XP Level System", "✓ დავალებების და XP დონეების სისტემა") },
        { "MockTomatoes", ("Heirloom Tomatoes", "ორგანული პომიდორი") },
        { "MockAiQuote", ("Prune lower suckers this week to concentrate fruit set. Water early morning.", "მოაჭერით ქვედა ყლორტები ამ კვირაში მოსავლიანობის გასაზრდელად. მორწყეთ დილით ადრე.") },
        { "MockTaskTitle", ("Prune lower branches & apply mulch", "ქვედა ტოტების გასხვლა და მულჩირება") },
        { "WhyDargeTitle", ("Why Modern Growers Choose DARGE", "რატომ ირჩევენ ფერმერები DARGE-ს") },
        { "WhyDargeSub", ("Four powerful modules designed to maximize harvest yield and eliminate guesswork.", "ოთხი ძლიერი მოდული მოსავლიანობის მაქსიმიზაციისა და მოვლის გაადვილებისთვის.") },
        { "Feature1Title", ("AI Project Planning", "AI პროექტების დაგეგმვა") },
        { "Feature1Desc", ("Specify your space (m²), climate, and soil type. DARGE AI generates custom step-by-step growing plans.", "მიუთითეთ ფართობი (მ²), კლიმატი და ნიადაგი. DARGE AI შექმნის პერსონალურ გეგმას.") },
        { "Feature2Title", ("Structured Tasks", "სტრუქტურირებული დავალებები") },
        { "Feature2Desc", ("Execute sequential care steps, track deadlines, and receive XP rewards upon task completion.", "შეასრულეთ თანმიმდევრული დავალებები და მიიღეთ XP ქულები.") },
        { "Feature3Title", ("Contextual DARGE AI", "კონტექსტური DARGE AI") },
        { "Feature3Desc", ("Ask questions in English or Georgian and let DARGE AI propose direct project task updates.", "დასვით კითხვები ქართულად ან ინგლისურად და მიიღეთ AI-სგან დავალებების განახლების შეთავაზებები.") },
        { "Feature4Title", ("Gamified Progress & XP", "გეიმიფიცირებული პროგრესი და XP") },
        { "Feature4Desc", ("Level up your status from Beginner to Master Gardener while unlocking achievement milestones.", "გაზარდეთ თქვენი დონე დამწყებიდან ოსტატამდე და გახსენით ბეჯები.") },
        { "WorkflowTitle", ("How DARGE Transforms Your Garden", "როგორ გარდაქმნის DARGE თქვენს ბაღს") },
        { "WorkflowSub", ("From initial space assessment to peak harvest in four simple steps.", "სივრცის შეფასებიდან უხვ მოსავალამდე ოთხ მარტივ ნაბიჯში.") },
        { "Step1Title", ("1. Input Your Setup", "1. შეიყვანეთ მონაცემები") },
        { "Step1Desc", ("Share your location, available square meters (m²), soil type, and crop goals.", "გაგვიზიარეთ თქვენი მდებარეობა, ფართობი (მ²), ნიადაგი და მიზნები.") },
        { "Step2Title", ("2. Receive AI Plan", "2. მიიღეთ AI გეგმა") },
        { "Step2Desc", ("Review AI-generated tasks, care guidelines, and recommended watering schedules.", "განიხილეთ AI-ის მიერ გენერირებული დავალებები და რეკომენდაციები.") },
        { "Step3Title", ("3. Complete Tasks & Earn XP", "3. შეასრულეთ დავალებები") },
        { "Step3Desc", ("Mark daily actions complete to earn XP and advance your gardener level status.", "შეასრულეთ ყოველდღიური მოქმედებები და გაზარდეთ თქვენი XP დონე.") },
        { "Step4Title", ("4. Consult AI Anytime", "4. მიიღეთ AI რჩევები") },
        { "Step4Desc", ("Ask DARGE AI questions anytime or approve AI-proposed project task additions.", "დასვით კითხვები ნებისმიერ დროს და დაამტკიცეთ AI-ს შეთავაზებები.") },
        { "CtaTitle", ("Ready to Build Your Smart Garden?", "მზად ხართ ჭკვიანი ბაღის შესაქმნელად?") },
        { "CtaSub", ("Join thousands of growers leveraging AI assistance in English and Georgian.", "შემოუერთდით ათასობით მებაღეს, რომლებიც იყენებენ AI ასისტენტს.") },

        // Profile & Auth
        { "ProfileTitle", ("Profile & Account", "პროფილი და ანგარიში") },
        { "AchievementsUnlocked", ("Achievements Unlocked", "მიღწეული მიზნები") },
        { "BackToDashboard", ("Back to Dashboard", "მართვის პანელზე დაბრუნება") },
        { "WelcomeBackSub", ("Welcome back to DARGE AgriTech platform", "მოგესალმებით DARGE-ს პლატფორმაზე") },
        { "CreateYourAccount", ("Create Account", "ანგარიშის შექმნა") },
        { "Username", ("Username", "მომხმარებლის სახელი") },
        { "Password", ("Password", "პაროლი") }
    };

    public string Get(string key, string? culture = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return "";
        string cur = (culture ?? System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName).ToLower();
        if (Translations.TryGetValue(key.Trim(), out var val))
        {
            return cur == "ka" ? val.Ka : val.En;
        }
        return key;
    }
}
