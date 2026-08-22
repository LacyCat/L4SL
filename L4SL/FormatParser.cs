using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace L4SL.Feature;

/// <summary>
/// "died by {ev.Attacker.Nickname}" 같은 포맷 문자열을,
/// 이벤트 인자 타입(argType) 기준으로 미리 검증된 세그먼트 리스트로 변환한다.
/// 세그먼트는 문자열 리터럴 또는 List&lt;MemberInfo&gt;(멤버 체인) 둘 중 하나.
/// add/configure 시점에 한 번만 파싱해서 캐싱해두고, 이벤트 발생 시엔 값만 꺼내 쓴다.
/// </summary>
internal static class FormatParser
{
    public static List<object> Parse(string format, Type argType)
    {
        if (string.IsNullOrEmpty(format))
            throw new FormatException("포맷이 비어있습니다.");

        var segments = new List<object>();
        int i = 0;
        while (i < format.Length)
        {
            int open = format.IndexOf('{', i);
            if (open < 0)
            {
                segments.Add(format[i..]);
                break;
            }
            if (open > i)
                segments.Add(format[i..open]);

            int close = format.IndexOf('}', open);
            if (close < 0)
                throw new FormatException($"닫는 '}}' 가 없습니다: {format}");

            string token = format[(open + 1)..close];
            segments.Add(ResolveChain(token, argType));
            i = close + 1;
        }
        return segments;
    }

    private static List<MemberInfo> ResolveChain(string token, Type argType)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new FormatException("빈 { } 토큰입니다.");

        var parts = token.Split('.');
        int start = parts[0].Equals("ev", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        var chain = new List<MemberInfo>();
        Type current = argType;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

        for (int i = start; i < parts.Length; i++)
        {
            string name = parts[i];

            MemberInfo member =
                (MemberInfo)current.GetProperty(name, flags)
                ?? (MemberInfo) current.GetField(name, flags)
                ?? current.GetMethods(flags).FirstOrDefault(m =>
                       m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                       && m.GetParameters().Length == 0
                       && m.ReturnType != typeof(void));

            if (member == null)
                throw new FormatException($"'{current.Name}'에 '{name}' 필드/프로퍼티/무인자 함수가 없습니다 (토큰: {{{token}}})");

            chain.Add(member);
            current = member switch
            {
                PropertyInfo p => p.PropertyType,
                FieldInfo f => f.FieldType,
                MethodInfo m => m.ReturnType,
                _ => current
            };
        }
        return chain;
    }

    /// <summary>세그먼트 체인을 실제 이벤트 인자 객체(ev)에 적용해서 값을 뽑아낸다.</summary>
    public static object Resolve(List<MemberInfo> chain, object root)
    {
        object current = root;
        foreach (var member in chain)
        {
            if (current == null) return "null";
            current = member switch
            {
                PropertyInfo p => p.GetValue(current),
                FieldInfo f => f.GetValue(current),
                MethodInfo m => m.Invoke(current, null),
                _ => null
            };
        }
        return current ?? "null";
    }
}
