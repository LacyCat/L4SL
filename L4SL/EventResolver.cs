using System;
using System.Linq;
using System.Reflection;

namespace L4SL.Feature;

/// <summary>
/// "Player.Died" 같은 주소 문자열을 Exiled.Events.Handlers 네임스페이스의
/// 실제 static 프로퍼티(Event / Event&lt;T&gt; 반환)로 매핑한다.
/// </summary>
internal static class EventResolver
{
    // 어셈블리 전체 스캔은 비용이 크니 최초 1회만 하고 캐싱
    private static readonly Lazy<Type[]> HandlerTypes = new(() =>
        typeof(Exiled.Events.Handlers.Server).Assembly
            .GetTypes()
            .Where(t => t.Namespace == "Exiled.Events.Handlers" && t.IsClass)
            .ToArray());

    public static PropertyInfo Resolve(string address)
    {
        var parts = address.Split('.');
        if (parts.Length != 2)
            throw new ArgumentException($"주소 형식이 잘못됐습니다. 'ClassName.EventName' 형태여야 합니다: {address}");

        Type declaringType = HandlerTypes.Value
            .FirstOrDefault(t => string.Equals(t.Name, parts[0], StringComparison.OrdinalIgnoreCase));

        if (declaringType == null)
            throw new ArgumentException($"'{parts[0]}' 핸들러 클래스를 찾을 수 없습니다.");

        // 진짜 CLR event가 아니라 Event/Event<T>를 반환하는 static 프로퍼티이므로 GetProperty 사용
        PropertyInfo property = declaringType.GetProperty(parts[1],
            BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);

        if (property == null)
            throw new ArgumentException($"'{declaringType.Name}'에 '{parts[1]}' 이벤트가 없습니다.");

        return property;
    }
}
