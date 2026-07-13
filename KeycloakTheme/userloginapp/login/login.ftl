<#import "template.ftl" as layout>
<@layout.registrationLayout displayInfo=social.displayInfo displayWide=(realm.password && social.providers??); section>

<#if section = "header">
    ${msg("loginTitleHtml",(realm.displayNameHtml!''))?no_esc}

<#elseif section = "form">
    <div class="login-wrapper">
        <div class="login-card">

            <div class="login-header">
                <div class="login-logo">&#128274;</div>
                <h2>User &amp; Access Control</h2>
                <p class="login-subtitle">Sign in to your account</p>
            </div>

            <#if message?has_content>
                <div class="alert alert-${(message.type == 'error')?then('danger', message.type)} mb-3" role="alert">
                    ${kcSanitize(message.summary)?no_esc}
                </div>
            </#if>

            <form id="kc-form-login" action="${url.loginAction}" method="post">

                <div class="mb-3">
                    <label for="username" class="form-label">
                        <#if !realm.loginWithEmailAllowed>
                            ${msg("username")}
                        <#elseif !realm.registrationEmailAsUsername>
                            ${msg("usernameOrEmail")}
                        <#else>
                            ${msg("email")}
                        </#if>
                    </label>
                    <input
                        id="username"
                        name="username"
                        class="form-control"
                        type="text"
                        autofocus
                        autocomplete="username"
                        value="${(login.username!'')?html}"
                        placeholder="Enter username"
                    />
                </div>

                <div class="mb-3">
                    <label for="password" class="form-label">${msg("password")}</label>
                    <input
                        id="password"
                        name="password"
                        class="form-control"
                        type="password"
                        autocomplete="current-password"
                        placeholder="Enter password"
                    />
                </div>

                <#if realm.rememberMe && !usernameEditDisabled??>
                    <div class="mb-3 form-check">
                        <input
                            class="form-check-input"
                            type="checkbox"
                            id="rememberMe"
                            name="rememberMe"
                            <#if login.rememberMe??>checked</#if>
                        />
                        <label class="form-check-label" for="rememberMe">
                            ${msg("rememberMe")}
                        </label>
                    </div>
                </#if>

                <input type="hidden" id="id-hidden-input" name="credentialId"
                    <#if auth.selectedCredential?has_content>value="${auth.selectedCredential}"</#if>
                />

                <div class="d-grid mt-4">
                    <input
                        class="btn btn-primary btn-lg"
                        name="login"
                        id="kc-login"
                        type="submit"
                        value="${msg("doLogIn")}"
                    />
                </div>

                <#if realm.resetPasswordAllowed>
                    <div class="text-center mt-3">
                        <a href="${url.loginResetCredentialsUrl}" class="text-muted small">
                            ${msg("doForgotPassword")}
                        </a>
                    </div>
                </#if>

            </form>
        </div>
    </div>
</#if>

</@layout.registrationLayout>
