import type { NextConfig } from "next";
import createNextIntlPlugin from "next-intl/plugin";

const withNextIntl = createNextIntlPlugin("./src/i18n/request.ts");

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

export default withNextIntl(nextConfig);
