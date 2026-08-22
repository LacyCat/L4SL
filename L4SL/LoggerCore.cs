using System;
using System.Collections.Generic;
using System.Reflection;
using Exiled.Events.Features;

namespace L4SL.Feature;

/// <summary>
/// 활성 로거 하나의 런타임 핸들. Config에는 절대 저장하지 말 것
/// (Delegate/MethodInfo/object 등 직렬화 불가능한 리플렉션 핸들을 들고 있음 - 런타임 전용).
/// </summary>
internal sealed class ActiveLogger
{
    public string Address = null!;
    public string Format = null!;
    public bool IsVoid;              // true면 인자 없는 Event, false면 Event<T>
    public Type? ArgType;            // IsVoid면 null
    public object EventInstance = null!;
    public MethodInfo UnsubscribeMethod = null!;
    public Delegate Handler = null!;
    public object LoggerInstanceObj = null!;
    public MethodInfo UpdateFormatMethod = null!; // Event<T>: SetSegments(List<object>) / Void: SetFormat(string)
}

/// <summary>
/// address/format으로부터 실제 이벤트 구독까지 수행하는 정적 팩토리.
///
/// 두 가지 이벤트 형태를 모두 지원한다:
/// - Event&lt;T&gt; (인자 있음, 예: Player.Died): priority=int.MinValue로 항상 마지막 실행 보장.
///   Event&lt;T&gt;.Subscribe(handler, priority)는 priority 내림차순 삽입 구조라 이게 가능함.
/// - Event (인자 없음, 예: Server.RoundStarted): priority 개념 자체가 없고 단순 델리게이트 append.
///   포맷에 {ev...} 참조 대상이 없으므로 리터럴 문자열만 허용.
/// </summary>
internal static class LoggerCore
{
    public static ActiveLogger Create(string address, string format)
    {
        PropertyInfo eventProperty = EventResolver.Resolve(address);
        Type eventPropertyType = eventProperty.PropertyType;

        object? eventInstance = eventProperty.GetValue(null);
        if (eventInstance == null)
            throw new InvalidOperationException($"'{address}'의 이벤트 인스턴스가 아직 초기화되지 않았습니다.");

        bool isGenericEvent = eventPropertyType.IsGenericType && eventPropertyType.GetGenericTypeDefinition() == typeof(Event<>);
        bool isVoidEvent = eventPropertyType == typeof(Event);

        if (!isGenericEvent && !isVoidEvent)
            throw new InvalidOperationException($"'{address}'는 지원하는 형태의 이벤트가 아닙니다.");

        return isGenericEvent
            ? CreateGeneric(address, format, eventPropertyType, eventInstance)
            : CreateVoid(address, format, eventInstance);
    }

    private static ActiveLogger CreateGeneric(string address, string format, Type eventGenericType, object eventInstance)
    {
        Type argType = eventGenericType.GetGenericArguments()[0];
        List<object> segments = FormatParser.Parse(format, argType); // 오타/잘못된 필드는 여기서 바로 걸러짐

        // CustomEventHandler<T>를 정확히 조립해서 오버로드 모호성 없이 바로 시그니처로 찾음
        Type handlerDelegateType = typeof(CustomEventHandler<>).MakeGenericType(argType);

        MethodInfo subscribeMethod = eventGenericType.GetMethod("Subscribe", new[] { handlerDelegateType, typeof(int) })
            ?? throw new InvalidOperationException($"'{eventGenericType.Name}'에서 Subscribe(handler, priority) 오버로드를 찾을 수 없습니다.");

        MethodInfo unsubscribeMethod = eventGenericType.GetMethod("Unsubscribe", new[] { handlerDelegateType })
            ?? throw new InvalidOperationException($"'{eventGenericType.Name}'에서 Unsubscribe(handler) 오버로드를 찾을 수 없습니다.");

        Type instanceType = typeof(LoggerInstance<>).MakeGenericType(argType);
        object loggerInstanceObj = Activator.CreateInstance(instanceType, address, segments)!;
        MethodInfo handleMethod = instanceType.GetMethod("Handle", BindingFlags.Public | BindingFlags.Instance)!;
        MethodInfo setSegmentsMethod = instanceType.GetMethod("SetSegments", BindingFlags.Public | BindingFlags.Instance)!;

        Delegate handler = Delegate.CreateDelegate(handlerDelegateType, loggerInstanceObj, handleMethod);

        // priority=int.MinValue -> 내림차순 정렬 구조상 항상 리스트 맨 끝에 삽입됨 (영구적으로 마지막 실행)
        subscribeMethod.Invoke(eventInstance, new object[] { handler, int.MinValue });

        return new ActiveLogger
        {
            Address = address,
            Format = format,
            IsVoid = false,
            ArgType = argType,
            EventInstance = eventInstance,
            UnsubscribeMethod = unsubscribeMethod,
            Handler = handler,
            LoggerInstanceObj = loggerInstanceObj,
            UpdateFormatMethod = setSegmentsMethod
        };
    }

    private static ActiveLogger CreateVoid(string address, string format, object eventInstance)
    {
        if (format.Contains('{') || format.Contains('}'))
            throw new InvalidOperationException($"'{address}'는 인자가 없는 이벤트라 {{}} 를 쓸 수 없습니다. 리터럴 문자열만 가능합니다.");

        Type handlerDelegateType = typeof(CustomEventHandler); // 제네릭 아님

        MethodInfo subscribeMethod = typeof(Event).GetMethod("Subscribe", new[] { handlerDelegateType })
            ?? throw new InvalidOperationException("Event.Subscribe(CustomEventHandler) 오버로드를 찾을 수 없습니다.");

        MethodInfo unsubscribeMethod = typeof(Event).GetMethod("Unsubscribe", new[] { handlerDelegateType })
            ?? throw new InvalidOperationException("Event.Unsubscribe(CustomEventHandler) 오버로드를 찾을 수 없습니다.");

        var loggerInstanceObj = new LoggerInstanceVoid(address, format);
        MethodInfo handleMethod = typeof(LoggerInstanceVoid).GetMethod(nameof(LoggerInstanceVoid.Handle))!;
        MethodInfo setFormatMethod = typeof(LoggerInstanceVoid).GetMethod(nameof(LoggerInstanceVoid.SetFormat))!;

        Delegate handler = Delegate.CreateDelegate(handlerDelegateType, loggerInstanceObj, handleMethod);

        // priority 개념 자체가 없음 - 그냥 맨 뒤에 append됨 (순서 보장 요구사항 없음, 라운드 시작류가 대상)
        subscribeMethod.Invoke(eventInstance, new object[] { handler });

        return new ActiveLogger
        {
            Address = address,
            Format = format,
            IsVoid = true,
            ArgType = null,
            EventInstance = eventInstance,
            UnsubscribeMethod = unsubscribeMethod,
            Handler = handler,
            LoggerInstanceObj = loggerInstanceObj,
            UpdateFormatMethod = setFormatMethod
        };
    }

    /// <summary>구독은 유지한 채 포맷만 교체 (재구독 없음 -> 순서/priority에 전혀 영향 없음).</summary>
    public static void Configure(ActiveLogger logger, string newFormat)
    {
        if (logger.IsVoid)
        {
            if (newFormat.Contains('{') || newFormat.Contains('}'))
                throw new InvalidOperationException($"'{logger.Address}'는 인자가 없는 이벤트라 {{}} 를 쓸 수 없습니다.");

            logger.UpdateFormatMethod.Invoke(logger.LoggerInstanceObj, new object[] { newFormat });
        }
        else
        {
            List<object> segments = FormatParser.Parse(newFormat, logger.ArgType!);
            logger.UpdateFormatMethod.Invoke(logger.LoggerInstanceObj, new object[] { segments });
        }

        logger.Format = newFormat;
    }

    public static void Destroy(ActiveLogger logger)
    {
        logger.UnsubscribeMethod.Invoke(logger.EventInstance, new object[] { logger.Handler });
    }
}
