import { Box, Typography } from "@mui/material";
import { responseHeadersStyles } from "./ResponseHeaders.styles";

type ResponseHeadersProps = {
  headers: Record<string, string[]>;
};

export function ResponseHeaders({ headers }: ResponseHeadersProps) {
  const entries = Object.entries(headers);

  if (entries.length === 0) {
    return <Typography variant="body2">No response headers.</Typography>;
  }

  return (
    <Box component="dl" sx={responseHeadersStyles.list}>
      {entries.map(([name, values]) => (
        <Box key={name} sx={responseHeadersStyles.item}>
          <Box component="dt" sx={responseHeadersStyles.name}>{name}</Box>
          <Box component="dd" sx={responseHeadersStyles.values}>
            {values.map((value, index) => <div key={`${name}-${index}`}>{value}</div>)}
          </Box>
        </Box>
      ))}
    </Box>
  );
}
