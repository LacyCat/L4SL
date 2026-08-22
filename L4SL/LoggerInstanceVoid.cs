using Exiled.API.Features;

namespace L4SL.Feature;

/// <summary>
/// 인자가 없는 이벤트(Exiled.Events.Features.Event, 예: RoundStarted)용.
/// 포맷에 {ev...} 토큰을 쓸 대상 자체가 없으므로 리터럴 문자열을 그대로 찍는다.
/// </summary>
internal sealed class LoggerInstanceVoid
{
    private readonly string _address;
    private string _format;

    public LoggerInstanceVoid(string address, string format)
    {
        _address = address;
        _format = format;
    }

    // configure 명령에서 리플렉션으로 호출됨.
    public void SetFormat(string format) => _format = format;

    public void Handle() => Log.Info($"[L4SL:{_address}] {_format}");
}
