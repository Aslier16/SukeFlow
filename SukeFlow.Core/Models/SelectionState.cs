namespace SukeFlow.Core.Models;

/// <summary>教务系统中该课程的选课状态（来自 HTML 中 font 颜色）。</summary>
public enum SelectionState
{
    /// <summary>未知。</summary>
    Unknown,

    /// <summary>已选上（蓝色）。</summary>
    Selected,

    /// <summary>待筛选（红色斜体）。</summary>
    Pending,
}
