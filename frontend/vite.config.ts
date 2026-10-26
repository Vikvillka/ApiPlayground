import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

const apiOrigin = process.env.APIPLAYGROUND_API_ORIGIN ?? "http://localhost:5000";
const browserHost = process.env.APIPLAYGROUND_VITE_HOST ?? "127.0.0.1";

export default defineConfig({
  plugins: [react()],
  server: {
    host: browserHost,
    proxy: {
      "/api": {
      target: apiOrigin,
      changeOrigin: true,
      },
    },
  },
  test: {
    include: ["src/**/*.test.ts", "src/**/*.test.tsx"],
    environment: "jsdom",
    setupFiles: ["./src/test-setup.ts"],
  },
});