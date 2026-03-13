import Link from 'next/link';

const links = [
  ['/dashboard', 'Dashboard'],
  ['/catalog', 'Cmdlet catalog'],
  ['/dlp/policies', 'DLP policies'],
  ['/dlp/rules', 'DLP rules'],
  ['/dlp/sits', 'SITs'],
  ['/dlp/keyword-dictionaries', 'Keyword dictionaries'],
  ['/dlp/rule-packages', 'Rule packages'],
  ['/classification/test-text-extraction', 'Test text extraction'],
  ['/classification/test-data-classification', 'Test data classification'],
  ['/messaging/test-message', 'Test message'],
  ['/jobs', 'Job history / results'],
  ['/audit', 'Audit log'],
  ['/admin-settings', 'Admin settings']
] as const;

export function Nav() {
  return <nav className="nav">{links.map(([href, label]) => <div key={href}><Link href={href}>{label}</Link></div>)}</nav>;
}
