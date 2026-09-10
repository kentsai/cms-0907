// Production build (`ng build`; angular.json defaultConfiguration = production).
// `/api` is deliberately relative: the deployed SPA and API are same-origin. On IIS the site
// "CMS" (:80) reverse-proxies /api/* to the API site via URL Rewrite + ARR (deploy\CMS.NG\web.config.template),
// so the browser never talks to the API port directly and no CORS policy is involved.
// Local development uses environment.development.ts (absolute http://localhost:5000/api, no proxy).
export const environment = {
  production: true,
  apiBaseUrl: '/api'
};
