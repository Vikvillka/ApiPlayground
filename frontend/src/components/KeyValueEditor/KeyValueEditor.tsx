import { Box, Button, Checkbox, FormControlLabel, IconButton, TextField, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import type { RequestRow } from "../../request/requestHelpers";
import { keyValueEditorStyles } from "./KeyValueEditor.styles";

type KeyValueEditorProps = {
  noun: "Parameter" | "Header";
  rows: RequestRow[];
  disabled: boolean;
  keyError: string | null;
  onChange: (id: number, change: Partial<RequestRow>) => void;
  onAdd: () => void;
  onRemove: (id: number) => void;
};

export function KeyValueEditor({ noun, rows, disabled, keyError, onChange, onAdd, onRemove }: KeyValueEditorProps) {
  const singular = noun.toLowerCase();
  const errorId = `${singular}-error`;

  return (
    <Box sx={keyValueEditorStyles.root}>
      {rows.map(row => {
        const redactedWithoutValue = row.redacted === true && !row.value;
        return (
          <Box key={row.id} sx={keyValueEditorStyles.row}>
            <TextField
              size="small"
              placeholder="Key"
              value={row.key}
              disabled={disabled}
              error={Boolean(keyError)}
              onChange={event => onChange(row.id, { key: event.target.value, redacted: false })}
              slotProps={{
                htmlInput: {
                  "aria-label": `${noun} key`,
                  "aria-describedby": keyError ? errorId : undefined,
                },
              }}
            />
            <TextField
              size="small"
              placeholder={row.redacted ? "Value not saved — enter it again" : "Value"}
              value={row.value}
              disabled={disabled}
              onChange={event =>
                onChange(row.id, {
                  value: event.target.value,
                  enabled: row.redacted ? Boolean(event.target.value) : row.enabled,
                })
              }
              slotProps={{ htmlInput: { "aria-label": `${noun} value` } }}
            />
            <FormControlLabel
              label="Enabled"
              control={
                <Checkbox
                  checked={row.enabled}
                  disabled={disabled || redactedWithoutValue}
                  onChange={event => onChange(row.id, { enabled: event.target.checked })}
                />
              }
            />
            <IconButton
              type="button"
              aria-label={`Remove ${singular}`}
              disabled={disabled}
              onClick={() => onRemove(row.id)}
              size="small"
              sx={keyValueEditorStyles.remove}
            >
              <DeleteOutlineIcon fontSize="small" />
            </IconButton>
          </Box>
        );
      })}
      {keyError && (
        <Typography id={errorId} role="alert" color="error" variant="body2">
          {keyError}
        </Typography>
      )}
      <Button
        type="button"
        onClick={onAdd}
        disabled={disabled}
        startIcon={<AddIcon />}
        sx={keyValueEditorStyles.add}
      >
        Add {singular}
      </Button>
    </Box>
  );
}
