using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Exiled.API.Features;

namespace L4SL.Feature;

/// <summary>
/// 인자가 있는 이벤트(Event&lt;T&gt;)용 로그 출력 본체. Handle(T ev)이 델리게이트 타깃 메서드가 된다.
/// </summary>
internal sealed class LoggerInstance<T>
{
    private readonly string _address;
    private List<object> _segments;

    public LoggerInstance(string address, List<object> segments)
    {
        _address = address;
        _segments = segments;
    }

    // configure 명령에서 리플렉션으로 호출됨 (content = List<object> 세그먼트).
    public void SetContent(object content) => _segments = (List<object>)content;

    public void Handle(T ev)
    {
        var sb = new StringBuilder();
        foreach (var seg in _segments)
        {
            if (seg is string literal)
                sb.Append(literal);
            else
                sb.Append(FormatParser.Resolve((List<MemberInfo>)seg, ev));
        }
        Log.Info($"[L4SL:{_address}] {sb}");
    }
}

/// <summary>
/// 인자가 없는 이벤트(Event, 비제네릭)용 로그 출력 본체. 꺼낼 필드가 없으므로 리터럴 문자열만 찍는다.
/// </summary>
internal sealed class LoggerInstanceNoArgs
{
    private readonly string _address;
    private string _literal;

    public LoggerInstanceNoArgs(string address, string literal)
    {
        _address = address;
        _literal = literal;
    }

    // configure 명령에서 리플렉션으로 호출됨 (content = string 리터럴).
    public void SetContent(object content) => _literal = (string)content;

    public void Handle() => Log.Info($"[L4SL:{_address}] {_literal}");
}
