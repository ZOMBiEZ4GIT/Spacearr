import React from 'react';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';

function SpacearrHistoryPage() {
  return (
    <PageContent title="History">
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
          History coming soon
        </div>
      </PageContentBody>
    </PageContent>
  );
}

export default SpacearrHistoryPage;
