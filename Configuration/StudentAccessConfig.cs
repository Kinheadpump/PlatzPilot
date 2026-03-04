using System;
using System.Collections.Generic;

namespace PlatzPilot.Configuration;

public sealed class StudentAccessConfig
{
    public TimeSpan Start { get; set; } = new(7, 0, 0);
    public TimeSpan End { get; set; } = new(22, 0, 0);
    public List<string> LocationIds { get; set; } = [];
    public List<string> BuildingIds { get; set; } = [];
    public List<string> NameContains { get; set; } = [];
}