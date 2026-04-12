import { defineConfig } from 'tsup';
import pkg from './package.json';

export default defineConfig({
  entry: ['src/index.ts'],
  format: ['cjs', 'esm', 'iife'],
  globalName: 'mcollector', 
  env: {
    __SDK_VERSION__: pkg.version
  },
  dts: true, 
  sourcemap: true,
  clean: true, 
  minify: true, 
  target: 'es2020',
  outExtension({ format }: { format: 'cjs' | 'esm' | 'iife' }) {
    if (format === 'iife') return { js: '.global.js' };
    return { js: `.${format === 'esm' ? 'mjs' : 'js'}` };
  }
});
