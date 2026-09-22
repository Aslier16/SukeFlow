using System;
using SukeFlow.Core.Models;

namespace SukeFlow.Core.Controls;

/// <summary>点击课程卡片时的事件参数。</summary>
public sealed class CourseTappedEventArgs : EventArgs
{
    public CourseTappedEventArgs(Course course, CourseSession session)
    {
        Course = course;
        Session = session;
    }

    /// <summary>被点击的课程。</summary>
    public Course Course { get; }

    /// <summary>被点击的时段。</summary>
    public CourseSession Session { get; }
}
