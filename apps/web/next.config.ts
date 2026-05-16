import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  async rewrites() {
    const apiUrl = process.env.NEXT_PUBLIC_BASE_URL || "http://localhost:5003";
    return [
      {
        source: "/api/auth/passkey/:path*",
        destination: `${apiUrl}/api/auth/passkey/:path*`,
      },
    ];
  },
};

export default nextConfig;
