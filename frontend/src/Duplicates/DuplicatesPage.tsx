import React from 'react';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';

function DuplicatesPage() {
  return (
    <PageContent title="Duplicates">
      <PageContentBody>
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            height: '300px',
            color: 'var(--disabledColor)',
            fontSize: '18px',
          }}
        >
          Duplicates coming soon
        </div>
      </PageContentBody>
    </PageContent>
  );
}

export default DuplicatesPage;
