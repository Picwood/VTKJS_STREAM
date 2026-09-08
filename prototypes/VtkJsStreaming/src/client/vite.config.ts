import { defineConfig } from 'vitest/config';

const maximumJavaScriptChunkBytes = 500_000;

export default defineConfig({
  plugins: [
    {
      name: 'development-csp-style-injection',
      apply: 'serve',
      transformIndexHtml(html) {
        return html.replace("style-src 'self'", "style-src 'self' 'unsafe-inline'");
      },
    },
    {
      name: 'production-javascript-chunk-budget',
      apply: 'build',
      generateBundle(_options, bundle) {
        const oversized = Object.values(bundle).filter((output) =>
          output.type === 'chunk' && output.code.length > maximumJavaScriptChunkBytes);
        if (oversized.length > 0) {
          this.error(`JavaScript chunks exceed the ${maximumJavaScriptChunkBytes}-byte budget: ${oversized
            .map((output) => `${output.fileName} (${output.type === 'chunk' ? output.code.length : 0} bytes)`)
            .join(', ')}`);
        }
      },
    },
  ],
  server: {
    host: '127.0.0.1',
    port: 5173,
  },
  build: {
    rolldownOptions: {
      output: {
        strictExecutionOrder: true,
        codeSplitting: {
          groups: [
            {
              name: 'vtk',
              test: /node_modules[\\/]@kitware[\\/]vtk\.js/,
              priority: 20,
              maxSize: 350_000,
            },
            {
              name: 'vendor',
              test: /node_modules/,
              priority: 10,
              maxSize: 350_000,
            },
          ],
        },
      },
    },
  },
  test: {
    environment: 'node',
  },
});
