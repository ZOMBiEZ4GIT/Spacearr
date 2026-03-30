import React from 'react';
import Label from 'Components/Label';
import CustomFormat from 'typings/CustomFormat';

interface MovieFormatsProps {
  formats: CustomFormat[];
}

function MovieFormats({ formats }: MovieFormatsProps) {
  return (
    <span>
      {formats.map((format) => (
        <Label key={format.id}>{format.name}</Label>
      ))}
    </span>
  );
}

export default MovieFormats;
