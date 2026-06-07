import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");
  const apiTarget = env.SUPPORTPILOT_API_TARGET || "http://localhost:5295";

  return {
    plugins: [react()],
    server: {
      port: 5173,
      proxy: {
        "/api": {
          target: apiTarget,
          changeOrigin: true
        },
        "/hubs": {
          target: apiTarget,
          changeOrigin: true,
          ws: true
        }
      }
    }
  };
});
