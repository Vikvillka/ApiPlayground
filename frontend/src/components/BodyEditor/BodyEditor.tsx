import { Box, TextField, Typography } from "@mui/material";
import { bodyEditorStyles } from "./BodyEditor.styles";

type BodyEditorProps = {
  value: string;
  disabled: boolean;
  error: string | null;
  onChange: (value: string) => void;
};

export function BodyEditor({ value, disabled, error, onChange }: BodyEditorProps) {
  return (
    <Box sx={bodyEditorStyles.root}>
      <TextField
        id="json-body"
        label="JSON body"
        multiline
        rows={8}
        value={value}
        disabled={disabled}
        error={Boolean(error)}
        onChange={event => onChange(event.target.value)}
        slotProps={{ htmlInput: { "aria-describedby": error ? "body-error" : "body-hint" } }}
      />
      {error ? (
        <Typography id="body-error" role="alert" color="error" variant="body2">
          {error}
        </Typography>
      ) : (
        <Typography id="body-hint" variant="body2" color="text.secondary">
          Optional JSON; available for POST, PUT, and PATCH.
        </Typography>
      )}
    </Box>
  );
}
