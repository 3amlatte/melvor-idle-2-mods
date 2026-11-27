using System;
using System.Collections.Generic;
using System.Linq;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Components.Actions;
using GamesByMalcs.Melvor2.Components.ArtisanAction;
using GamesByMalcs.Melvor2.Components.MasteryGuild;
using GamesByMalcs.Melvor2.Components.Trackers;
using GamesByMalcs.Melvor2.Data;
using GamesByMalcs.Melvor2.Data.Recipes;
using GamesByMalcs.Melvor2.Entities;
using GamesByMalcs.Melvor2.Entities.Cooking;
using GamesByMalcs.Melvor2.Goals;
using Godot;
using MelvorModLoader;

namespace MasteryEta;

/// <summary>
/// Adds a second tracker row next to the game's action bar. It looks at every skill that has a
/// Mastery Guild, finds the one whose current tracked action belongs to an incomplete task
/// ("Cook 400 Beef", "Create 400 Rune Defenders"), and shows the task, progress, icon and ETA.
/// </summary>
public sealed class MasteryEtaMod : IMod
{
    sealed class Candidate
    {
        public required ActiveMasteryGuildTask Task;
        public required ArtisanRecipe Recipe;
        public required IMasteryGuildEntity Entity;
        public required int Priority;
    }

    ModContext _ctx = null!;
    MasteryBar _bar = null!;
    TrackerComponentFooterView? _footer;
    Game? _game;
    int _frame;

    ActiveMasteryGuildTask? _task;
    EventHandler<GoalProgressedEventArgs>? _progressHandler;
    double _lastProgress;
    long _lastProgressMs;
    double _rate;

    string _loggedReason = "";
    string _loggedError = "";
    string _loggedAction = "";
    string _loggedTask = "";
    bool _footerLogged;
    string _loggedNoMatchAction = "";

    public void OnLoad(ModContext ctx)
    {
        _ctx = ctx;
        _bar = new MasteryBar(ctx);
        ctx.Frame += OnFrame;
        ctx.Log("Loaded.");
    }

    void OnFrame()
    {
        if (++_frame % 15 != 0) return;   // four times a second
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
                    _ctx.Log($"Mastery ETA cannot read the tracker ({e.GetType().Name}: {e.Message}); using skill trackers.");
                }
            }
        }

        Candidate? best = FindCandidate(game, footerTracker);
        if (best == null)
        {
            LogNoCandidate(game);
            Hide("no current action with an incomplete mastery task");
            return;
        }

        IActiveGoal goal = best.Task.ActiveGoal!;
        double current = goal.CurrentProgress;
        double max = goal.MaxProgress;
        double remaining = Math.Max(0, max - current);

        if (_loggedAction != $"{best.Entity.DisplayName.Text}:{best.Recipe.DisplayName.Text}")
        {
            _loggedAction = $"{best.Entity.DisplayName.Text}:{best.Recipe.DisplayName.Text}";
            _ctx.Log($"Mastery ETA action: {_loggedAction}; task {BuildTaskText(best.Task, best.Recipe, current, max)}.");
        }

        BindTask(best.Task);

        string eta = FormatEta(remaining, best.Recipe);
        string text = BuildTaskText(best.Task, best.Recipe, current, max);
        Texture2D? icon = best.Task.Task.Media ?? best.Recipe.Icon;

        _bar.Show(_footer, icon, text, current, max, eta);
        if (_loggedTask != text)
        {
            _loggedTask = text;
            _ctx.Log($"Mastery ETA showing: {text}, {eta}.");
        }
    }

    Candidate? FindCandidate(Game game, TrackerComponent? footerTracker)
    {
        Candidate? best = null;

        foreach (GameEntity entity in game.Entities.GetAllObjects())
        {
            if (entity is not IMasteryGuildEntity mastery) continue;

            TrackerComponent? tracker = entity.Components.Values
                .OfType<TrackerComponent>()
                .FirstOrDefault();

            foreach (ActionComponent action in entity.Components.Values.OfType<ActionComponent>())
            {
                ArtisanRecipe? recipe = CurrentRecipe(action);
                if (recipe == null) continue;

                ActiveMasteryGuildTask? task = FindTask(mastery.MasteryGuild, recipe);
                if (task?.ActiveGoal == null) continue;

                bool producing = action.GetType().GetProperty("CreatingRecipe")?.GetValue(action) != null;
                int priority = (action.IsStarted ? 2 : 0)
                             + (producing ? 1 : 0)
                             + (ReferenceEquals(tracker, footerTracker) ? 4 : 0);

                if (best == null || priority > best.Priority)
                {
                    best = new Candidate
                    {
                        Task = task,
                        Recipe = recipe,
                        Entity = mastery,
                        Priority = priority
                    };
                }
            }
        }

        return best;
    }

    void LogNoCandidate(Game game)
    {
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

    void Hide(string reason)
    {
        _bar.Hide();
        if (_loggedReason != reason)
        {
            _loggedReason = reason;
            _ctx.Log($"Mastery ETA hidden: {reason}.");
        }
        _loggedTask = "";
        UnbindTask();
    }

    void BindTask(ActiveMasteryGuildTask task)
    {
        if (ReferenceEquals(task, _task)) return;

        UnbindTask();
        _task = task;
        _progressHandler ??= OnProgress;
        if (task.ActiveGoal != null)
        {
            task.ActiveGoal.Progressed += _progressHandler;
        }

        _rate = 0;
        _lastProgress = task.ActiveGoal?.CurrentProgress ?? 0;
        _lastProgressMs = (long)Time.GetTicksMsec();
        _loggedReason = "";
    }

    void UnbindTask()
    {
        if (_task?.ActiveGoal != null && _progressHandler != null)
        {
            try { _task.ActiveGoal.Progressed -= _progressHandler; } catch { /* old game */ }
        }
        _task = null;
        _rate = 0;
        _lastProgress = 0;
        _lastProgressMs = 0;
    }

    void OnProgress(object? sender, GoalProgressedEventArgs e)
    {
        if (_task?.ActiveGoal is not { } goal) return;

        long now = (long)Time.GetTicksMsec();
        double progress = goal.CurrentProgress;

        if (_lastProgressMs > 0)
        {
            double seconds = (now - _lastProgressMs) / 1000.0;
            double gained = progress - _lastProgress;
            if (seconds > 0.2 && gained > 0)
            {
                double instant = gained / seconds;
                _rate = _rate <= 0 ? instant : _rate * 0.7 + instant * 0.3;
            }
        }

        _lastProgress = progress;
        _lastProgressMs = now;
    }

    string FormatEta(double remaining, ArtisanRecipe recipe)
    {
        if (remaining <= 0) return "complete";

        long now = (long)Time.GetTicksMsec();
        if (_lastProgressMs > 0 && now - _lastProgressMs > 120_000)
        {
            return "paused";
        }

        if (_rate > 0)
        {
            return FormatSeconds(remaining / _rate) + " left";
        }

        // No measured rate yet: estimate from the recipe's base interval, one progress per
        // action, and mark it approximate.
        double fallback = remaining * Math.Max(1, recipe.BaseInterval) / 1000.0;
        return "~" + FormatSeconds(fallback) + " left";
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

    string BuildTaskText(ActiveMasteryGuildTask active, ArtisanRecipe recipe, double current, double max)
    {
        if (active.Task.CustomDescription is { } custom && !string.IsNullOrWhiteSpace(custom.Text))
        {
            return $"{custom.Text} ({current:N0} / {max:N0})";
        }

        string verb = active.Task.Goal is CookRecipeGoal ? "Cook" : "Create";
        string name = active.Task.Goal switch
        {
            CookRecipeGoal c => c.Recipe?.DisplayName.Text
                                ?? c.Subcategory?.DisplayName.Text
                                ?? (c.Category is MasteryGuildCategory ? "products" : c.Category?.DisplayName.Text)
                                ?? "products",
            CreateRecipeGoal a => a.Recipe?.DisplayName.Text
                                  ?? a.Subcategory?.DisplayName.Text
                                  ?? (a.Category is MasteryGuildCategory ? "items" : a.Category?.DisplayName.Text)
                                  ?? "items",
            _ => recipe.DisplayName.Text
        };

        return $"{verb} {current:N0} / {max:N0} {name}";
    }

    static ActiveMasteryGuildTask? FindTask(MasteryGuildComponent guild, ArtisanRecipe recipe)
    {
        ActiveMasteryGuildTask? best = null;
        int bestScore = -1;
        double bestRemaining = double.MaxValue;

        foreach (ActiveMasteryGuildTask active in guild.ActiveTasks.Values)
        {
            if (active.IsComplete || active.ActiveGoal == null) continue;

            int score = MatchScore(active.Task.Goal, recipe);
            if (score < 0) continue;

            double remaining = active.ActiveGoal.MaxProgress - active.ActiveGoal.CurrentProgress;
            if (score > bestScore || (score == bestScore && remaining < bestRemaining))
            {
                best = active;
                bestScore = score;
                bestRemaining = remaining;
            }
        }

        return best;
    }

    static int MatchScore(Goal? goal, ArtisanRecipe recipe)
    {
        // The task's Category is the Mastery Guild tier (Novice/Easy/...), not the recipe
        // category, so it is deliberately not compared here. Recipe first, then subcategory,
        // then any product of the skill.
        if (goal is CookRecipeGoal cook)
        {
            if (recipe is not CookingRecipe cooking) return -1;
            if (cook.Recipe != null) return cook.Recipe == cooking ? 3 : -1;
            if (cook.Subcategory != null) return cooking.Subcategory == cook.Subcategory ? 2 : -1;
            return 1;
        }

        if (goal is CreateRecipeGoal create)
        {
            if (recipe is CookingRecipe) return -1;
            if (create.Recipe != null) return create.Recipe == recipe ? 3 : -1;
            if (create.Subcategory != null) return recipe.Subcategory == create.Subcategory ? 2 : -1;
            return 1;
        }

        return -1;
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
