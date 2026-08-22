using System.Collections.Generic;
using Exiled.API.Interfaces;

namespace L4SL;

public sealed class Config : IConfig
{
    public bool Debug { get; set; } = false;
    public bool IsEnabled { get; set; } = true;

    public List<LoggerEntry> Loggers { get; set; } = new();
}

public sealed class LoggerEntry
{
    public string Address { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
}
