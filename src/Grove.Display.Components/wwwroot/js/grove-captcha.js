// The form captcha (docs/widgets.md): loads the ReCaptcha script once, explicitly rendered, and
// hands each token (or its expiry) to the Captcha component.
let loading;

function load(scriptUri) {
    if (window.grecaptcha && window.grecaptcha.render) {
        return Promise.resolve();
    }

    if (!loading) {
        loading = new Promise((resolve, reject) => {
            window.groveCaptchaLoaded = () => resolve();
            const element = document.createElement('script');
            element.src = scriptUri + (scriptUri.indexOf('?') >= 0 ? '&' : '?') + 'onload=groveCaptchaLoaded&render=explicit';
            element.async = true;
            element.defer = true;
            element.onerror = reject;
            document.head.appendChild(element);
        });
    }

    return loading;
}

export async function render(element, siteKey, scriptUri, component) {
    await load(scriptUri);
    return window.grecaptcha.render(element, {
        sitekey: siteKey,
        callback: token => component.invokeMethodAsync('SetToken', token),
        'expired-callback': () => component.invokeMethodAsync('SetToken', null),
        'error-callback': () => component.invokeMethodAsync('SetToken', null),
    });
}

export function reset(widget) {
    if (window.grecaptcha && widget !== null && widget !== undefined) {
        window.grecaptcha.reset(widget);
    }
}
