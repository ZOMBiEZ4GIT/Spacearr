import React from 'react';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';

function RecommendationsPage() {
  return (
    <PageContent title="Recommendations">
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
          Recommendations coming soon
        </div>
      </PageContentBody>
    </PageContent>
  );
}

export default RecommendationsPage;
