using System.Collections.Generic;

namespace MineSweeper.Models;

/// <summary>One answer option for a quiz question.</summary>
public sealed class QuizOption
{
    public string Value    { get; set; } = string.Empty;
    public bool   IsAnswer { get; set; }
}

/// <summary>A single multiple-choice quiz question loaded from <c>real_data.json</c>.</summary>
public sealed class QuizQuestion
{
    public int              Id       { get; set; }
    public string           Question { get; set; } = string.Empty;
    public List<QuizOption> Options  { get; set; } = new();
}
