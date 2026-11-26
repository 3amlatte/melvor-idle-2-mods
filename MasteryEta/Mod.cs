using System;
using GamesByMalcs.Melvor2;
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
/// Adds a second tracker row next to the game's action bar. When the current action belongs
/// to a Mastery Guild task ("Cook 400 Beef", "Create 400 Rune Defenders"), it shows the task,
/// its progress, the action icon and the estimated time to finish it.
/// </summary>
public sealed class MasteryEtaMod : IMod
{
    ModContext _ctx = null!;
    MasteryBar _bar = null!;
    TrackerComponentFooterView? _footer;
    int _frame;

    string _loggedReason = "";
    string _loggedError = "";
    bool _trackerBroken;
    ActiveMasteryGuildTask? _task;
    EventHandler<GoalProgressedEventArgs>? _progressHandler;
    double _lastProgress;
    long _lastProgressMs;
    double _rate;

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
        if (_footer == null || !GodotObject.IsInstanceValid(_footer) || !_footer.IsInsideTree())
        {
            _footer = FindFooter(_ctx.Tree.Root);
        }

        if (_footer == null)
        {
            Hide("tracker footer not found");
            return;
        }

        if (_trackerBroken)
        {
            _bar.Hide();
            return;
        }

        if (!_footer.IsVisibleInTree())
        {
            Hide("tracker footer is hidden");
            return;
        }

        TrackerComponent? tracker;
        try
        {
            tracker = Accessors.TrackerOf(_footer);
        }
        catch (Exception e)
        {
            _trackerBroken = true;
            _ctx.Log($"Mastery ETA cannot read the tracker ({e.GetType().Name}: {e.Message}); the bar is off.");
            _bar.Hide();
            return;
        }

        if (tracker == null)
        {
            Hide("no tracker");
            return;
        }

        if (tracker.ActionDescription is not ArtisanRecipe recipe)
        {
            Hide("current action is not a recipe");
            return;
        }

        if (tracker.Entity is not IMasteryGuildEntity mastery)
        {
            Hide("skill has no Mastery Guild");
            return;
        }

        ActiveMasteryGuildTask? best = FindTask(mastery.MasteryGuild, recipe);
        if (best?.ActiveGoal == null)
        {
            Hide("no incomplete mastery task for this action");
            return;
        }

        BindTask(best);

        IActiveGoal goal = best.ActiveGoal;
        double current = goal.CurrentProgress;
        double max = goal.MaxProgress;
        double remaining = Math.Max(0, max - current);

        string eta = FormatEta(remaining, recipe, current, max);
        string text = BuildTaskText(best, recipe, current, max);
        Texture2D? icon = best.Task.Media ?? recipe.Icon;

        _bar.Show(_footer, icon, text, current, max, eta);
    }

    void Hide(string reason)
    {
        _bar.Hide();
        if (_task != null && _loggedReason != reason)
        {
            _loggedReason = reason;
            _ctx.Log($"Mastery ETA hidden: {reason}.");
        }
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

    string FormatEta(double remaining, ArtisanRecipe recipe, double current, double max)
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
                                ?? c.Category?.DisplayName.Text
                                ?? recipe.DisplayName.Text,
            CreateRecipeGoal a => a.Recipe?.DisplayName.Text
                                  ?? a.Subcategory?.DisplayName.Text
                                  ?? a.Category?.DisplayName.Text
                                  ?? recipe.DisplayName.Text,
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
        if (goal is CookRecipeGoal cook)
        {
            if (recipe is not CookingRecipe cooking) return -1;
            if (cook.Recipe != null) return cook.Recipe == cooking ? 3 : -1;
            if (cook.Subcategory != null && cooking.Subcategory != cook.Subcategory) return -1;
            if (cook.Category != null && cooking.Category != cook.Category) return -1;
            return cook.Subcategory != null || cook.Category != null ? 2 : 1;
        }

        if (goal is CreateRecipeGoal create)
        {
            if (recipe is CookingRecipe) return -1;
            if (create.Recipe != null) return create.Recipe == recipe ? 3 : -1;
            if (create.Subcategory != null && recipe.Subcategory != create.Subcategory) return -1;
            if (create.Category != null && recipe.Category != create.Category) return -1;
            return create.Subcategory != null || create.Category != null ? 2 : 1;
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
