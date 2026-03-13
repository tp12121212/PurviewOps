import './globals.css';
import { Nav } from '../components/Nav';

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body>
        <div className="container">
          <Nav />
          <main className="main">{children}</main>
        </div>
      </body>
    </html>
  );
}
