using System.Text.Json;

namespace AuroraAudioStudio.Services;

public static class WorkbenchReadiness
{
    public static bool IsReady(string json, string instance)
    {
        if (string.IsNullOrWhiteSpace(instance)) return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("aurora_instance").GetString() != instance) return false;
            var components = root.GetProperty("components").EnumerateArray().ToArray();
            var audio = components.Where(c => c.GetProperty("type").GetString() == "audio")
                .Select(c => c.GetProperty("id").GetInt32()).ToHashSet();
            return components.Any(c => c.GetProperty("type").GetString() == "button")
                && root.GetProperty("dependencies").EnumerateArray().Any(d =>
                    d.GetProperty("backend_fn").ValueKind == JsonValueKind.True
                    && d.GetProperty("inputs").GetArrayLength() > 0
                    && d.GetProperty("outputs").EnumerateArray().Any(id => audio.Contains(id.GetInt32())));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { return false; }
    }

    // ExecuteScriptAsync returns JSON; poll synchronous DOM state, not a JavaScript Promise.
    public static string DomScript(string instance) => """
        (() => {
          const c = window.gradio_config;
          if (!c || c.aurora_instance !== __INSTANCE__) return false;
          const app = document.querySelector('gradio-app');
          const root = app?.shadowRoot || document;
          const shown = e => e && e.getClientRects().length > 0 && getComputedStyle(e).visibility !== 'hidden';
          const element = component => root.getElementById(component.props?.elem_id || 'component-' + component.id);
          const components = c.components || [];
          const buttons = new Set((c.dependencies || []).filter(d => d.backend_fn)
            .flatMap(d => (d.targets || []).filter(t => t[1] === 'click').map(t => t[0])));
          const buttonReady = components.some(c => c.type === 'button' && buttons.has(c.id) &&
            (() => { const e = element(c); const b = e?.matches('button') ? e : e?.querySelector('button'); return shown(b) && !b.disabled; })());
          const inputReady = [...root.querySelectorAll('textarea,input:not([type=hidden]),[contenteditable=true]')]
            .some(e => shown(e) && !e.disabled);
          const outputs = new Set((c.dependencies || []).filter(d => d.backend_fn && d.inputs?.length)
            .flatMap(d => d.outputs || []));
          const outputReady = components.some(c => c.type === 'audio' && outputs.has(c.id) && shown(element(c)));
          return buttonReady && inputReady && outputReady;
        })()
        """.Replace("__INSTANCE__", JsonSerializer.Serialize(instance));
}
