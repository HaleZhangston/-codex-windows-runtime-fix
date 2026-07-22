using System.Text.Json;

namespace CampusNetAutoLogin;

internal static class PortalAutomation
{
    public static string BuildScript(string username, string password, bool autoSubmit)
    {
        string userJson = JsonSerializer.Serialize(username);
        string passJson = JsonSerializer.Serialize(password);
        string submitJson = autoSubmit ? "true" : "false";

        return $$"""
        (() => {
          const username = {{userJson}};
          const password = {{passJson}};
          const autoSubmit = {{submitJson}};
          const visible = el => !!el && !el.disabled && el.type !== 'hidden';
          const first = selectors => {
            for (const selector of selectors) {
              const el = [...document.querySelectorAll(selector)].find(visible);
              if (el) return el;
            }
            return null;
          };
          const setValue = (el, value) => {
            const proto = el instanceof HTMLTextAreaElement
              ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
            const setter = Object.getOwnPropertyDescriptor(proto, 'value')?.set;
            if (setter) setter.call(el, value); else el.value = value;
            el.dispatchEvent(new Event('input', { bubbles: true }));
            el.dispatchEvent(new Event('change', { bubbles: true }));
          };
          const user = first([
            'input[name="DDDDD"]', '#DDDDD',
            'input[name="username"]', '#username', '#userName', '#account',
            'input[name="user"]', 'input[name="account"]',
            'input[autocomplete="username"]', 'input[type="text"]'
          ]);
          const pass = first([
            'input[name="upass"]', '#upass',
            'input[name="password"]', '#password', '#pwd',
            'input[autocomplete="current-password"]', 'input[type="password"]'
          ]);
          if (!user || !pass) {
            return JSON.stringify({ status: 'fields-not-found', title: document.title, url: location.href });
          }
          setValue(user, username);
          setValue(pass, password);
          if (!autoSubmit) {
            pass.focus();
            return JSON.stringify({ status: 'filled', title: document.title, url: location.href });
          }
          const submit = first([
            'input[name="0MKKey"]', '#0MKKey', '#login', '#loginBtn', '#submit',
            'button[type="submit"]', 'input[type="submit"]',
            'button.login', '.login-button', '.btn-login'
          ]);
          if (submit) submit.click();
          else if (pass.form?.requestSubmit) pass.form.requestSubmit();
          else if (pass.form) pass.form.submit();
          else return JSON.stringify({ status: 'filled-no-submit', title: document.title, url: location.href });
          return JSON.stringify({ status: 'submitted', title: document.title, url: location.href });
        })();
        """;
    }
}
