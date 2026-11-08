import type { Metadata } from 'next';
import './globals.css';
import { AuthProvider } from '@/lib/authContext';
import { ToastProvider } from '@/components/Toast';

export const metadata: Metadata = {
  title: 'ACUDGeoManager | منصة إدارة المرافق الجغرافية',
  description: 'Enterprise Web GIS Platform for New Administrative Capital Multi-Utility Networks',
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="ar" dir="rtl">
      <body className="h-screen w-screen overflow-hidden bg-slate-50 text-slate-900 antialiased">
        <AuthProvider>
          <ToastProvider>
            {children}
          </ToastProvider>
        </AuthProvider>
      </body>
    </html>
  );
}
