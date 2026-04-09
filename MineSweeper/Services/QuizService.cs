using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text.Json;
using BugsFinder.Models;

namespace BugsFinder.Services;

/// <summary>
/// Loads quiz questions from the embedded <c>real_data.json</c> resource and
/// vends them in a random, non-repeating order (shuffled pool that refills once
/// exhausted so every question is seen before any repeats).
/// </summary>
public static class QuizService
{
    private static readonly List<QuizQuestion> _all  = [];
    private static readonly List<int>          _pool = [];
    private static bool _loaded;

    /// <summary>
    /// Loads the question bank from the embedded resource (idempotent).
    /// Safe to call multiple times; actual I/O happens only on the first call.
    /// </summary>
    public static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            var asm    = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(
                "BugsFinder.QuizGenerator.real_data.json");
            if (stream is null) return;

            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var list = JsonSerializer.Deserialize<List<QuizQuestion>>(stream, opts);
            if (list is null) return;

            foreach (var q in list)
            {
                q.Question = WebUtility.HtmlDecode(q.Question);
                foreach (var o in q.Options)
                    o.Value = WebUtility.HtmlDecode(o.Value);
                _all.Add(q);
            }
        }
        catch { /* silently ignore load failures */ }
    }

    /// <summary>
    /// Returns a random <see cref="QuizQuestion"/>, cycling through the entire
    /// pool before any question repeats. Returns <see langword="null"/> only if
    /// the question bank failed to load.
    /// </summary>
    public static QuizQuestion? GetRandom()
    {
        EnsureLoaded();
        if (_all.Count == 0) return null;

        if (_pool.Count == 0)
            for (int i = 0; i < _all.Count; i++) _pool.Add(i);

        int slot = Random.Shared.Next(_pool.Count);
        int qi   = _pool[slot];
        _pool.RemoveAt(slot);
        return _all[qi];
    }
}
