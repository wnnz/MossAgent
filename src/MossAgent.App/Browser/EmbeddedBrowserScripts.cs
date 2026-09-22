using System.Text.Json;

namespace MossAgent.App.Browser;

internal static class EmbeddedBrowserScripts
{
    public const string PageText = "document.body?.innerText ?? ''";

    public const string Snapshot = """
        (() => {
          const lines = [`# ${document.title || 'Untitled'}`, `URL: ${location.href}`];
          const nodes = document.querySelectorAll(
            'a,button,input,textarea,select,[role],[contenteditable="true"],h1,h2,h3');
          for (const node of nodes) {
            const role = node.getAttribute('role') || node.tagName.toLowerCase();
            const name = node.getAttribute('aria-label')
              || node.innerText || node.value || node.getAttribute('placeholder') || '';
            const text = String(name).replace(/\s+/g, ' ').trim();
            if (text) lines.push(`${role}: ${text.slice(0, 500)}`);
          }
          lines.push('', document.body?.innerText ?? '');
          return lines.join('\n').slice(0, 100000);
        })()
        """;

    public static string Click(string selector)
    {
        var encoded = JsonSerializer.Serialize(selector);
        return $$"""
            (() => {
              const element = document.querySelector({{encoded}});
              if (!element) throw new Error('Selector not found');
              element.click();
              return true;
            })()
            """;
    }

    public static string Type(string selector, string text)
    {
        var encodedSelector = JsonSerializer.Serialize(selector);
        var encodedText = JsonSerializer.Serialize(text);
        return $$"""
            (() => {
              const element = document.querySelector({{encodedSelector}});
              if (!element) throw new Error('Selector not found');
              element.focus();
              if ('value' in element) element.value = {{encodedText}};
              else element.textContent = {{encodedText}};
              element.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertText', data: {{encodedText}} }));
              element.dispatchEvent(new Event('change', { bubbles: true }));
              return true;
            })()
            """;
    }

    public static string SelectOption(string selector, string value)
    {
        var encodedSelector = JsonSerializer.Serialize(selector);
        var encodedValue = JsonSerializer.Serialize(value);
        return $$"""
            (() => {
              const element = document.querySelector({{encodedSelector}});
              if (!(element instanceof HTMLSelectElement)) throw new Error('Selector is not a select');
              const option = Array.from(element.options).find(
                item => item.value === {{encodedValue}} || item.label === {{encodedValue}});
              if (!option) throw new Error('Option not found');
              element.value = option.value;
              element.dispatchEvent(new Event('input', { bubbles: true }));
              element.dispatchEvent(new Event('change', { bubbles: true }));
              return true;
            })()
            """;
    }

    public static string MatchesState(string selector, string state)
    {
        var encodedSelector = JsonSerializer.Serialize(selector);
        var encodedState = JsonSerializer.Serialize(state);
        return $$"""
            (() => {
              const element = document.querySelector({{encodedSelector}});
              const state = {{encodedState}};
              if (state === 'Detached') return !element;
              if (state === 'Attached') return !!element;
              if (!element) return state === 'Hidden';
              const style = getComputedStyle(element);
              const rect = element.getBoundingClientRect();
              const visible = style.visibility !== 'hidden' && style.display !== 'none'
                && rect.width > 0 && rect.height > 0;
              return state === 'Visible' ? visible : !visible;
            })()
            """;
    }

    public static string Scroll(string? selector, double deltaX, double deltaY)
    {
        var encodedSelector = JsonSerializer.Serialize(selector);
        return $$"""
            (() => {
              const selector = {{encodedSelector}};
              const target = selector ? document.querySelector(selector) : window;
              if (!target) throw new Error('Selector not found');
              target.scrollBy({ left: {{deltaX}}, top: {{deltaY}}, behavior: 'auto' });
              return true;
            })()
            """;
    }

    public static string StartDownload(string selector, int maximumBytes)
    {
        var encodedSelector = JsonSerializer.Serialize(selector);
        return $$"""
            (() => {
              window.__mossDownload = { status: 'pending' };
              (async () => {
                try {
                  const element = document.querySelector({{encodedSelector}});
                  if (!element) throw new Error('Selector not found');
                  const rawUrl = element.href || element.getAttribute('href');
                  const url = new URL(rawUrl, location.href);
                  if (url.protocol !== 'http:' && url.protocol !== 'https:') {
                    throw new Error('Only HTTP(S) downloads are allowed');
                  }
                  const response = await fetch(url.href, { credentials: 'include' });
                  if (!response.ok || !response.body) throw new Error(`HTTP ${response.status}`);
                  const reader = response.body.getReader();
                  const chunks = [];
                  let total = 0;
                  while (true) {
                    const { done, value } = await reader.read();
                    if (done) break;
                    total += value.byteLength;
                    if (total > {{maximumBytes}}) throw new Error('Download exceeds size limit');
                    chunks.push(value);
                  }
                  const bytes = new Uint8Array(total);
                  let offset = 0;
                  for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.length; }
                  let binary = '';
                  for (let index = 0; index < bytes.length; index += 32768) {
                    binary += String.fromCharCode(...bytes.subarray(index, index + 32768));
                  }
                  const disposition = response.headers.get('content-disposition') || '';
                  const match = disposition.match(/filename\*?=(?:UTF-8''|\")?([^\";]+)/i);
                  const suggested = element.getAttribute('download')
                    || (match ? decodeURIComponent(match[1].trim()) : '')
                    || url.pathname.split('/').pop() || 'download.bin';
                  window.__mossDownload = { status: 'ready', suggested, data: btoa(binary) };
                } catch (error) {
                  window.__mossDownload = { status: 'error', error: String(error?.message || error) };
                }
              })();
              return true;
            })()
            """;
    }

    public const string DownloadStatus = "window.__mossDownload ?? { status: 'missing' }";

    public const string ClearDownload = "delete window.__mossDownload";
}
