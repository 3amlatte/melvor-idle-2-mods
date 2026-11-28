using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Components.Actions;
using GamesByMalcs.Melvor2.Components.ArtisanAction;
using GamesByMalcs.Melvor2.Components.MasteryGuild;
using GamesByMalcs.Melvor2.Components.Trackers;
using GamesByMalcs.Melvor2.Data;
using GamesByMalcs.Melvor2.Data.Recipes;
using GamesByMalcs.Melvor2.Entities;
using GamesByMalcs.Melvor2.Entities.Cooking;
using GamesByMalcs.Melvor2.GameMenus;
using GamesByMalcs.Melvor2.Goals;
using Godot;
using MelvorModLoader;

namespace MasteryEta;

/// <summary>
/// Adds compact mastery cells inside the game's action footer. One cell per active skill that
/// has an incomplete Mastery Guild task matching the action it is currently running, so two
/// skills training at once produce two cells along the bar.
/// </summary>
public sealed class MasteryEtaMod : IMod
{
    sealed class Candidate
    {
        public required ActiveMasteryGuildTask Task;
        public required IMasteryGuildEntity Entity;
        public required int Priority;
        public required int Score;
        public required double Remaining;
        public required double Ratio;
        public ArtisanRecipe? Recipe;
        public string ActionName = "";
        public Texture2D? Icon;
        public int BaseIntervalMs;
        public bool Passive;
        public bool Producing;
    }

    sealed class RateState
    {
        public required ActiveMasteryGuildTask Task;
        public EventHandler<GoalProgressedEventArgs>? Handler;
        public double LastProgress;
        public long LastMs;
        public double Rate;
        public double EtaSeconds;
        public long EtaMs;
        public int Logs;
    }

    const int MaxTasks = 4;

    ModContext _ctx = null!;
    MasteryBar _bar = null!;
    Texture2D? _iconSandwich;
    Texture2D? _iconSoup;
    Texture2D? _iconCake;
    Texture2D? _iconPancake;
    Texture2D? _iconPie;
    Texture2D? _iconSushi;
    TrackerComponentFooterView? _footer;
    Game? _game;
    int _frame;

    readonly Dictionary<ActiveMasteryGuildTask, RateState> _rates = new();
    string _loggedReason = "";
    string _loggedError = "";
    string _loggedTasks = "";
    string _loggedPlacement = "";
    string _loggedNoMatchAction = "";
    string _loggedTrackerAction = "";
    bool _footerLogged;

    public void OnLoad(ModContext ctx)
    {
        _ctx = ctx;
        _bar = new MasteryBar(ctx);
        string iconDir = Path.Combine(ctx.Directory, "icons");
        _iconSandwich = LoadIcon(iconDir, "sandwich");
        _iconSoup = LoadIcon(iconDir, "soup");
        _iconCake = LoadIcon(iconDir, "cake");
        _iconPancake = LoadIcon(iconDir, "pancake");
        _iconPie = LoadIcon(iconDir, "pie");
        _iconSushi = LoadIcon(iconDir, "sushi");

        ctx.Frame += OnFrame;
        ctx.Log("Loaded.");
    }

    void OnFrame()
    {
        if (++_frame % 6 != 0) return;   // about ten times a second
        try { Update(); }
        catch (Exception e)
        {
            string message = $"{e.GetType().Name}: {e.Message}";
            if (message != _loggedError)
            {
                _loggedError = message;
                _ctx.Log($"Mastery ETA update failed: {message}");
            }
            _bar.Hide();
        }
    }

    void Update()
    {
        Game? game = GameAccess.CurrentGame;
        if (game is not { IsSaveLoaded: true })
        {
            Hide("no save loaded");
            return;
        }
        _game = game;

        if (_footer == null || !GodotObject.IsInstanceValid(_footer) || !_footer.IsInsideTree())
        {
            _footer = FindFooter(_ctx.Tree.Root);
            if (_footer != null && !_footerLogged)
            {
                _footerLogged = true;
                _ctx.Log($"Mastery ETA found the tracker footer ({_footer.GetType().Name}).");
            }
        }

        TrackerComponent? footerTracker = null;
        if (_footer != null && GodotObject.IsInstanceValid(_footer))
        {
            try { footerTracker = Accessors.TrackerOf(_footer); }
            catch (Exception e)
            {
                if (_loggedError != "tracker-access")
                {
                    _loggedError = "tracker-access";
                    _ctx.Log($"Mastery ETA cannot read the tracker ({e.GetType().Name}: {e.Message}); using skill actions.");
                }
            }
        }

        List<Candidate> candidates = FindCandidates(game, footerTracker);
        SyncRates(candidates);

        if (candidates.Count == 0)
        {
            LogNoCandidate(game, footerTracker);
            Hide("no current action with an incomplete mastery task");
            return;
        }

        var entries = new List<MasteryBar.Entry>(candidates.Count);
        string signature = "";

        foreach (Candidate candidate in candidates)
        {
            RateState state = _rates[candidate.Task];
            IActiveGoal goal = candidate.Task.ActiveGoal!;
            double current = goal.CurrentProgress;
            double max = goal.MaxProgress;

            entries.Add(new MasteryBar.Entry(
                candidate.Icon,
                BuildTaskText(candidate, current, max),
                current,
                max,
                FormatEta(candidate, state),
                () => OpenMastery(candidate)));

            signature += candidate.Task.Task.Id + ";";
        }

        if (signature != _loggedTasks)
        {
            _loggedTasks = signature;
            _ctx.Log("Mastery ETA tasks: " + string.Join(" | ", candidates.Select(c =>
                $"{(c.Passive ? "passive " : "")}{c.Entity.DisplayName.Text}:{c.ActionName} -> " +
                $"{BuildTaskText(c, c.Task.ActiveGoal!.CurrentProgress, c.Task.ActiveGoal.MaxProgress)} " +
                $"[{c.Task.Task.Category?.DisplayName.Text ?? "?"}]")));

            foreach (var group in candidates.GroupBy(c => c.Entity))
            {
                var progressing = group.Key.MasteryGuild.ActiveTasks.Values
                    .Where(t => !t.IsComplete && t.ActiveGoal is { CurrentProgress: > 0 })
                    .OrderByDescending(t => t.ActiveGoal!.CurrentProgress / Math.Max(1, t.ActiveGoal.MaxProgress))
                    .Take(6)
                    .Select(t => $"{t.Task.Id} [{t.Task.Category?.DisplayName.Text}] " +
                                 $"{t.ActiveGoal!.CurrentProgress:N0}/{t.ActiveGoal.MaxProgress:N0}");
                _ctx.Log($"Mastery ETA progress tasks for {group.Key.DisplayName.Text}: " +
                         (progressing.Any() ? string.Join(", ", progressing) : "none"));
            }
        }

        _bar.Show(_footer, entries);

        if (_loggedPlacement != _bar.LastPlacement)
        {
            _loggedPlacement = _bar.LastPlacement;
            _ctx.Log($"Mastery ETA placement: {_loggedPlacement}.");
        }

        _loggedReason = "";
    }

    List<Candidate> FindCandidates(Game game, TrackerComponent? footerTracker)
    {
        var byTask = new Dictionary<ActiveMasteryGuildTask, Candidate>();

        void Add(Candidate candidate)
        {
            if (!byTask.TryGetValue(candidate.Task, out Candidate? existing) ||
                candidate.Priority > existing.Priority ||
                (candidate.Priority == existing.Priority && candidate.Score > existing.Score))
            {
                byTask[candidate.Task] = candidate;
            }
        }

        foreach (GameEntity entity in game.Entities.GetAllObjects())
        {
            if (entity is not IMasteryGuildEntity mastery) continue;

            TrackerComponent? tracker = entity.Components.Values
                .OfType<TrackerComponent>()
                .FirstOrDefault();
            bool trackerIsFooter = ReferenceEquals(tracker, footerTracker);

            var actions = entity.Components.Values.OfType<ActionComponent>().ToList();
            bool started = actions.Any(a => a.IsStarted);

            foreach (ActionComponent action in actions)
            {
                ArtisanRecipe? recipe = CurrentRecipe(action);
                if (recipe == null) continue;

                object? creating = action.GetType().GetProperty("CreatingRecipe")?.GetValue(action);
                bool producing = creating != null;
                bool passive = producing && !action.IsStarted;
                if (!producing && !action.IsStarted) continue;
                if (producing && !action.IsStarted && !ActionTimerRunning(action)) continue;

                int priority = (action.IsStarted ? 2 : 0) + (producing ? 1 : 0) + (trackerIsFooter ? 4 : 0);
                int interval = ActualInterval(creating) ?? recipe.BaseInterval;

                foreach (ActiveMasteryGuildTask task in mastery.MasteryGuild.ActiveTasks.Values)
                {
                    if (task.IsComplete || task.ActiveGoal == null) continue;
                    int score = MatchScore(task.Task.Goal, recipe);
                    if (score < 0) continue;

                    Add(new Candidate
                    {
                        Task = task,
                        Entity = mastery,
                        Priority = priority,
                        Score = score,
                        Remaining = task.ActiveGoal.MaxProgress - task.ActiveGoal.CurrentProgress,
                        Ratio = task.ActiveGoal.MaxProgress > 0
                            ? task.ActiveGoal.CurrentProgress / task.ActiveGoal.MaxProgress
                            : 0,
                        Recipe = recipe,
                        ActionName = recipe.DisplayName.Text,
                        Icon = ChooseIcon(task, recipe),
                        BaseIntervalMs = interval,
                        Passive = passive,
                        Producing = producing
                    });
                }
            }

            if (started && tracker?.ActionDescription is { } actionDescription &&
                actionDescription is not ArtisanRecipe)
            {
                int priority = (started ? 2 : 0) + (trackerIsFooter ? 4 : 0);

                foreach (ActiveMasteryGuildTask task in mastery.MasteryGuild.ActiveTasks.Values)
                {
                    if (task.IsComplete || task.ActiveGoal == null) continue;
                    if (!GoalMatchesAction(task.Task.Goal, actionDescription)) continue;

                    Add(new Candidate
                    {
                        Task = task,
                        Entity = mastery,
                        Priority = priority,
                        Score = 3,
                        Remaining = task.ActiveGoal.MaxProgress - task.ActiveGoal.CurrentProgress,
                        Ratio = task.ActiveGoal.MaxProgress > 0
                            ? task.ActiveGoal.CurrentProgress / task.ActiveGoal.MaxProgress
                            : 0,
                        Recipe = null,
                        ActionName = actionDescription.DisplayName.Text,
                        Icon = actionDescription.Icon ?? task.Task.Media,
                        BaseIntervalMs = 0,
                        Producing = false
                    });
                }
            }
        }

        var found = new List<Candidate>();

        foreach (var group in byTask.Values.GroupBy(c => c.Entity))
        {
            var active = group
                .Where(c => IsRecentlyUpdated(c.Task) || c.Ratio > 0.001)
                .OrderByDescending(c => IsRecentlyUpdated(c.Task))
                .ThenByDescending(c => c.Ratio)
                .ThenBy(c => c.Task.ActiveGoal!.MaxProgress)
                .ToList();

            if (active.Count == 0)
            {
                active.Add(group
                    .OrderByDescending(c => c.Ratio)
                    .ThenBy(c => c.Task.ActiveGoal!.MaxProgress)
                    .First());
            }

            // Up to two simultaneous masteries for one skill.
            found.AddRange(active.Take(2));
        }

        return found
            .OrderByDescending(c => c.Priority)
            .ThenByDescending(c => c.Score)
            .ThenByDescending(c => c.Ratio)
            .ThenBy(c => c.Task.ActiveGoal!.MaxProgress)
            .Take(MaxTasks)
            .ToList();
    }

    Texture2D? ChooseIcon(ActiveMasteryGuildTask task, ArtisanRecipe recipe)
    {
        // Keep the game's own task picture when it has one.
        if (HasRealMedia(task.Task.Media)) return task.Task.Media;

        // A task for one specific recipe keeps that item's icon.
        if (task.Task.Goal is CookRecipeGoal cook && cook.Recipe != null)
            return cook.Recipe.Icon ?? recipe.Icon;
        if (task.Task.Goal is CreateRecipeGoal create && create.Recipe != null)
            return create.Recipe.Icon ?? recipe.Icon;

        // Only category/subcategory tasks with no specific item use the generated icons.
        return TaskIcon(task.Task.Goal, recipe) ?? recipe.Icon;
    }

    static bool HasRealMedia(Texture2D? media)
    {
        if (media == null || media is PlaceholderTexture2D) return false;
        string path = media.ResourcePath ?? "";
        if (path.Length == 0 || path.Contains("missing_artwork", StringComparison.OrdinalIgnoreCase))
            return false;
        try
        {
            Vector2 size = media.GetSize();
            return size.X > 4 && size.Y > 4;
        }
        catch { return false; }
    }

    Texture2D? TaskIcon(Goal? goal, ArtisanRecipe recipe)
    {
        string key = "";
        if (goal is CookRecipeGoal cook)
        {
            key = (cook.Subcategory?.Id ?? "") + " " + (cook.Subcategory?.DisplayName.Text ?? "");
            if (key.Trim().Length == 0) key = cook.Recipe?.Subcategory?.Id ?? "";
        }
        if (key.Length == 0) key = recipe.Subcategory?.Id ?? "";
        key = key.ToLowerInvariant();

        if (key.Contains("pancake")) return _iconPancake;
        if (key.Contains("sandwich")) return _iconSandwich;
        if (key.Contains("sushi")) return _iconSushi;
        if (key.Contains("soup")) return _iconSoup;
        if (key.Contains("cake")) return _iconCake;
        if (key.Contains("pie")) return _iconPie;
        return null;
    }

    static Texture2D? LoadIcon(string directory, string name)
    {
        foreach (string extension in new[] { ".png", ".jpg", ".webp" })
        {
            string path = Path.Combine(directory, name + extension);
            if (!File.Exists(path)) continue;
            try
            {
                Image? image = Image.LoadFromFile(path);
                return image == null ? null : ImageTexture.CreateFromImage(image);
            }
            catch { return null; }
        }
        return null;
    }

    static bool IsRecentlyUpdated(ActiveMasteryGuildTask task) =>
        task.LastUpdated is { } updated &&
        (DateTime.UtcNow - updated).TotalMinutes <= 15;

    void OpenMastery(Candidate candidate)
    {
        try
        {
            GameView? view = FindGameView(_ctx.Tree.Root);
            if (view?.MasteryGuild == null)
            {
                _ctx.Log("Mastery ETA could not find the mastery window.");
                return;
            }

            view.CloseExclusiveModals();
            MasteryGuildComponent guild = candidate.Entity.MasteryGuild;
            if (candidate.Task.Task.Category != null)
            {
                guild.SelectedCategory = candidate.Task.Task.Category;
            }
            view.MasteryGuild.Bind(guild);
            view.MasteryGuild.Popup();
            _ctx.Log($"Mastery ETA opened {candidate.Entity.DisplayName.Text} Mastery Guild " +
                     $"({candidate.Task.Task.Category?.DisplayName.Text ?? "no category"}).");
        }
        catch (Exception e)
        {
            _ctx.Log($"Mastery ETA could not open the mastery window: {e.GetType().Name}: {e.Message}");
        }
    }

    static GameView? FindGameView(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is GameView view) return view;
            GameView? found = FindGameView(child);
            if (found != null) return found;
        }
        return null;
    }

    void SyncRates(List<Candidate> candidates)
    {
        var keep = new HashSet<ActiveMasteryGuildTask>(candidates.Select(c => c.Task));

        foreach (var pair in _rates.ToList())
        {
            if (keep.Contains(pair.Key)) continue;
            if (pair.Value.Task.ActiveGoal != null && pair.Value.Handler != null)
            {
                try { pair.Value.Task.ActiveGoal.Progressed -= pair.Value.Handler; } catch { }
            }
            _rates.Remove(pair.Key);
        }

        foreach (Candidate candidate in candidates)
        {
            if (_rates.ContainsKey(candidate.Task)) continue;

            var state = new RateState
            {
                Task = candidate.Task,
                LastProgress = candidate.Task.ActiveGoal?.CurrentProgress ?? 0,
                LastMs = (long)Time.GetTicksMsec()
            };
            state.Handler = (_, _) => OnProgress(state);

            if (candidate.Task.ActiveGoal is { } goal)
            {
                _ctx.Log($"Mastery ETA task state: {candidate.Task.Task.Id}, active={goal.IsActive}, " +
                         $"complete={goal.IsComplete}, progress={goal.CurrentProgress:N0}/{goal.MaxProgress:N0}.");
                if (!goal.IsActive && !goal.IsComplete)
                {
                    try { goal.SetActive(); _ctx.Log($"Mastery ETA activated inactive task {candidate.Task.Task.Id}."); }
                    catch { }
                }
                goal.Progressed += state.Handler;
            }

            _rates.Add(candidate.Task, state);
        }
    }

    void OnProgress(RateState state)
    {
        if (state.Task.ActiveGoal is not { } goal) return;

        long now = (long)Time.GetTicksMsec();
        double progress = goal.CurrentProgress;

        if (state.LastMs > 0)
        {
            double seconds = (now - state.LastMs) / 1000.0;
            double gained = progress - state.LastProgress;
            if (seconds > 0.2 && gained > 0)
            {
                double instant = gained / seconds;
                state.Rate = state.Rate <= 0 ? instant : state.Rate * 0.7 + instant * 0.3;
            }
        }

        state.LastProgress = progress;
        state.LastMs = now;

        if (state.Rate > 0)
        {
            state.EtaSeconds = Math.Max(0, goal.MaxProgress - progress) / state.Rate;
            state.EtaMs = now;
        }

        if (state.Logs < 5)
        {
            state.Logs++;
            _ctx.Log($"Mastery ETA progress: {state.Task.Task.Id} {progress:N0}/{goal.MaxProgress:N0}.");
        }
    }

    string FormatEta(Candidate candidate, RateState state)
    {
        double remaining = Math.Max(0, candidate.Task.ActiveGoal!.MaxProgress -
                                       candidate.Task.ActiveGoal.CurrentProgress);
        if (remaining <= 0) return "complete";

        long now = (long)Time.GetTicksMsec();
        if (!candidate.Producing && state.LastMs > 0 && now - state.LastMs > 120_000)
        {
            return "paused";
        }

        if (state.Rate <= 0 || state.EtaMs == 0) return "Calculating ETA";

        if (!candidate.Producing && state.LastMs > 0 && now - state.LastMs > 120_000)
        {
            return "paused";
        }

        double eta = state.EtaSeconds - (now - state.EtaMs) / 1000.0;
        return FormatSeconds(Math.Max(0, eta)) + " left";
    }

    static string FormatSeconds(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) return "--";
        if (seconds < 60) return $"{Math.Ceiling(seconds):0}s";
        if (seconds < 3600)
        {
            int m = (int)(seconds / 60);
            int s = (int)(seconds % 60);
            return $"{m}m {s:00}s";
        }
        if (seconds < 86400)
        {
            int h = (int)(seconds / 3600);
            int m = (int)((seconds % 3600) / 60);
            return $"{h}h {m:00}m";
        }
        int d = (int)(seconds / 86400);
        int hh = (int)((seconds % 86400) / 3600);
        return $"{d}d {hh:00}h";
    }

    string BuildTaskText(Candidate candidate, double current, double max)
    {
        ActiveMasteryGuildTask active = candidate.Task;

        string verb;
        if (active.Task.Goal is CookRecipeGoal) verb = "Cook";
        else if (active.Task.Goal is CreateRecipeGoal) verb = "Create";
        else
        {
            verb = "Mastery";
            if (active.ActiveGoal != null)
            {
                string description = StripBbcode(active.ActiveGoal.GetBaseLocalDescription());
                Match match = Regex.Match(description, @"^([A-Za-z]+)");
                if (match.Success) verb = match.Groups[1].Value;
            }
        }

        return $"{verb} {current:N0} / {max:N0}";
    }

    static string StripBbcode(string text)
    {
        string result = Regex.Replace(text, @"\[img[^\]]*\].*?\[/img\]", "", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\[[^\]]+\]", "");
        return Regex.Replace(result, @"\s+", " ").Trim();
    }

    static int MatchScore(Goal? goal, ArtisanRecipe recipe)
    {
        if (goal is CookRecipeGoal cook)
        {
            if (recipe is not CookingRecipe cooking) return -1;
            if (cook.Recipe != null) return cook.Recipe == cooking ? 3 : -1;
            if (!CategoryMatches(cook.Category, cooking.Category)) return -1;
            if (cook.Subcategory != null) return cooking.Subcategory == cook.Subcategory ? 2 : -1;
            return cook.Category is MasteryGuildCategory ? 1 : -1;
        }

        if (goal is CreateRecipeGoal create)
        {
            if (recipe is CookingRecipe) return -1;
            if (create.Recipe != null) return create.Recipe == recipe ? 3 : -1;
            if (!CategoryMatches(create.Category, recipe.Category)) return -1;
            if (create.Subcategory != null) return recipe.Subcategory == create.Subcategory ? 2 : -1;
            return create.Category is MasteryGuildCategory ? 1 : -1;
        }

        return -1;
    }

    static bool CategoryMatches(RecipeCategory? goalCategory, RecipeCategory? recipeCategory)
    {
        if (goalCategory == null) return true;
        // Novice/Easy/Adept... are Mastery Guild tiers, not recipe categories.
        if (goalCategory is MasteryGuildCategory) return true;
        return ReferenceEquals(goalCategory, recipeCategory) ||
               (recipeCategory != null && recipeCategory.Id == goalCategory.Id);
    }

    static bool ActionTimerRunning(ActionComponent action)
    {
        try
        {
            object? timer = action.GetType().GetProperty("ActionTimer")?.GetValue(action);
            object? running = timer?.GetType().GetProperty("IsRunning")?.GetValue(timer);
            return running is true;
        }
        catch { return false; }
    }

    static int? ActualInterval(object? creating)
    {
        if (creating == null) return null;
        try
        {
            object? stats = creating.GetType().GetProperty("Stats")?.GetValue(creating);
            object? node = stats?.GetType().GetProperty("Interval")?.GetValue(stats);
            object? value = node?.GetType().GetProperty("Value")?.GetValue(node);
            if (value is double d && d > 0) return (int)Math.Round(d);
            if (value is float f && f > 0) return (int)Math.Round(f);
        }
        catch { }
        return null;
    }

    static bool GoalMatchesAction(Goal? goal, IDescribedObject action)
    {
        if (goal == null) return false;

        foreach (string name in new[] { "Recipe", "Tree", "Rock", "Fish", "Course", "Obstacle" })
        {
            object? value = goal.GetType().GetProperty(name)?.GetValue(goal);
            if (value != null && SameData(value, action)) return true;
        }

        return false;
    }

    static bool SameData(object a, object b)
    {
        if (ReferenceEquals(a, b)) return true;
        string? idA = a.GetType().GetProperty("Id")?.GetValue(a) as string;
        string? idB = b.GetType().GetProperty("Id")?.GetValue(b) as string;
        return idA != null && idA == idB;
    }

    void Hide(string reason)
    {
        _bar.Hide();
        if (_loggedReason != reason)
        {
            _loggedReason = reason;
            _ctx.Log($"Mastery ETA hidden: {reason}.");
        }
        _loggedTasks = "";
        _loggedPlacement = "";
    }

    void LogNoCandidate(Game game, TrackerComponent? footerTracker)
    {
        if (footerTracker?.ActionDescription is { } tracked && tracked is not ArtisanRecipe)
        {
            string key = $"{footerTracker.Entity.DisplayName.Text}:{tracked.DisplayName.Text}";
            if (_loggedTrackerAction != key)
            {
                _loggedTrackerAction = key;
                _ctx.Log($"Mastery ETA tracker action: {key} (no matching mastery task).");
            }
        }

        foreach (GameEntity entity in game.Entities.GetAllObjects())
        {
            if (entity is not IMasteryGuildEntity mastery) continue;

            foreach (ActionComponent action in entity.Components.Values.OfType<ActionComponent>())
            {
                ArtisanRecipe? recipe = CurrentRecipe(action);
                if (recipe == null) continue;

                string key = $"{entity.DisplayName.Text}:{recipe.DisplayName.Text}";
                if (_loggedNoMatchAction == key) return;
                _loggedNoMatchAction = key;

                var incomplete = mastery.MasteryGuild.ActiveTasks.Values
                    .Where(t => !t.IsComplete)
                    .ToList();
                var goalTypes = incomplete
                    .Select(t => t.Task.Goal?.GetType().Name ?? "no goal")
                    .GroupBy(x => x)
                    .Select(g => $"{g.Key} x{g.Count()}");
                var scoreTypes = incomplete
                    .Select(t => MatchScore(t.Task.Goal, recipe))
                    .GroupBy(x => x)
                    .Select(g => $"{g.Key} x{g.Count()}");

                _ctx.Log($"Mastery ETA sees {key} but no task matched. " +
                         $"Incomplete tasks: {incomplete.Count}; goal types: {string.Join(", ", goalTypes)}; " +
                         $"match scores: {string.Join(", ", scoreTypes)}.");
                return;
            }
        }

        _loggedNoMatchAction = "";
    }

    static ArtisanRecipe? CurrentRecipe(ActionComponent action)
    {
        object? creating = action.GetType().GetProperty("CreatingRecipe")?.GetValue(action);
        return creating?.GetType().GetProperty("Recipe")?.GetValue(creating) as ArtisanRecipe;
    }

    static TrackerComponentFooterView? FindFooter(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is TrackerComponentFooterView footer) return footer;
            TrackerComponentFooterView? found = FindFooter(child);
            if (found != null) return found;
        }
        return null;
    }
}
