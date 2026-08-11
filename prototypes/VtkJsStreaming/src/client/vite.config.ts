import { defineConfig } from 'vitest/config';

export default defineConfig({
  plugins: [{
    name: 'development-csp-style-injection',
    apply: 'serve',
    transformIndexHtml(html) {
      return html.replace("style-src 'self'", "style-src 'self' 'unsafe-inline'");
    },
  }],
  server: {
    host: '127.0.0.1',
    port: 5173,
  },
  test: {
    environment: 'node',
  },
});
