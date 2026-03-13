import { PublicClientApplication } from '@azure/msal-browser';

export const msalInstance = new PublicClientApplication({
  auth: {
    clientId: process.env.NEXT_PUBLIC_AAD_CLIENT_ID ?? '<spa-client-id>',
    authority: process.env.NEXT_PUBLIC_AAD_AUTHORITY ?? 'https://login.microsoftonline.com/common',
    redirectUri: '/dashboard'
  },
  cache: {
    cacheLocation: 'sessionStorage'
  }
});

export const loginRequest = {
  scopes: ['openid', 'profile', 'email', 'api://<backend-app-id>/user_impersonation']
};
