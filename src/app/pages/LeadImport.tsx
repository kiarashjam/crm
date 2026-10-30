// Importing leads. The page is shared with contacts and companies (import/ImportPage);
// the server reads the file and does the import, as one batch that can be undone.
import ImportPage from './import/ImportPage';

export default function LeadImport() {
  return <ImportPage kind="lead" />;
}
