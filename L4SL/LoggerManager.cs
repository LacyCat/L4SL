using System;
using System.Collections.Concurrent;
using System.Linq;
using Exiled.API.Features;

namespace L4SL.Feature;

internal sealed class LoggerManager
{
    private Config Config => Main.Instance.Config;

    // 런타임 상태 (Delegate 등 직렬화 불가 - Config엔 address/format 문자열만 저장됨)
    private readonly ConcurrentDictionary<string, ActiveLogger> _active =
        new(StringComparer.OrdinalIgnoreCase);

    public string Add(string address, string format)
    {
        if (_active.ContainsKey(address))
            return $"이미 등록된 로거입니다: {address} (수정하려면 configure 사용)";

        ActiveLogger active;
        try
        {
            active = LoggerCore.Create(address, format);
        }
        catch (Exception ex)
        {
            return $"등록 실패 ({address}): {ex.Message}";
        }

        _active[address] = active;

        if (Config.Loggers.All(l => !l.Address.Equals(address, StringComparison.OrdinalIgnoreCase)))
            Config.Loggers.Add(new LoggerEntry { Address = address, Format = format });
        Main.Instance.SaveConfig();

        return $"등록됨: {address}: {format}";
    }

    public string Remove(string address)
    {
        if (!_active.TryRemove(address, out var active))
            return $"등록되어 있지 않습니다: {address}";

        try
        {
            LoggerCore.Destroy(active);
        }
        catch (Exception ex)
        {
            return $"해제 중 오류 ({address}): {ex.Message}";
        }

        Config.Loggers.RemoveAll(l => l.Address.Equals(address, StringComparison.OrdinalIgnoreCase));
        Main.Instance.SaveConfig();

        return $"해제됨: {address}";
    }

    public string Configure(string address, string newFormat)
    {
        if (!_active.TryGetValue(address, out var active))
            return $"등록되어 있지 않습니다: {address} (먼저 add로 등록하세요)";

        try
        {
            LoggerCore.Configure(active, newFormat);
        }
        catch (Exception ex)
        {
            return $"포맷 변경 실패 ({address}): {ex.Message}";
        }

        var entry = Config.Loggers.FirstOrDefault(l => l.Address.Equals(address, StringComparison.OrdinalIgnoreCase));
        if (entry != null)
            entry.Format = newFormat;
        Main.Instance.SaveConfig();

        return $"수정됨: {address}: {newFormat}";
    }

    public string List()
    {
        if (_active.IsEmpty)
            return "등록된 로거가 없습니다.";

        return string.Join("\n", _active.Values
            .OrderBy(a => a.Address, StringComparer.OrdinalIgnoreCase)
            .Select(a => $"{a.Address}: {a.Format}"));
    }

    /// <summary>Main.OnEnabled()에서 호출 - config에 저장된 로거들을 재구독.</summary>
    public void RestoreFromConfig()
    {
        foreach (var entry in Config.Loggers.ToList())
        {
            if (_active.ContainsKey(entry.Address))
                continue;

            try
            {
                var active = LoggerCore.Create(entry.Address, entry.Format);
                _active[entry.Address] = active;
            }
            catch (Exception ex)
            {
                Log.Error($"[L4SL] 복구 실패 ({entry.Address}): {ex.Message}");
            }
        }
    }

    /// <summary>Main.OnDisabled()에서 호출 - 남은 구독 전부 정리 (핫리로드 대비).</summary>
    public void RemoveAll()
    {
        foreach (var address in _active.Keys.ToList())
        {
            if (_active.TryRemove(address, out var active))
            {
                try { LoggerCore.Destroy(active); }
                catch (Exception ex) { Log.Error($"[L4SL] 해제 실패 ({address}): {ex.Message}"); }
            }
        }
    }
}
