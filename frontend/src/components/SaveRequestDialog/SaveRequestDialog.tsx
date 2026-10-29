import type { FormEvent, KeyboardEvent, RefObject } from "react";
import { observer } from "mobx-react-lite";
import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  FormControlLabel,
  FormLabel,
  NativeSelect,
  Radio,
  RadioGroup,
  TextField,
  Typography,
} from "@mui/material";
import { useRootStore } from "../../stores/StoreContext";
import { saveRequestDialogStyles } from "./SaveRequestDialog.styles";

const focusableSelector = 'button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled])';

type SaveRequestDialogProps = {
  // Focus returns here when the dialog closes.
  returnFocusRef: RefObject<HTMLButtonElement | null>;
};

// Rendered only while open, so the dialog unmounts immediately on close.
export const SaveRequestDialog = observer(function SaveRequestDialog({ returnFocusRef }: SaveRequestDialogProps) {
  const root = useRootStore();
  const { history, saveRequest } = root;

  if (!saveRequest.open) return null;

  function returnFocus() {
    requestAnimationFrame(() => returnFocusRef.current?.focus());
  }

  function closeDialog() {
    if (saveRequest.saving) return;
    saveRequest.close();
    returnFocus();
  }

  async function handleSave(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await saveRequest.save();
    if (!saveRequest.open) returnFocus();
  }

  // Keeps Tab and Shift+Tab inside the dialog.
  function handleKeyDown(event: KeyboardEvent<HTMLElement>) {
    if (event.key !== "Tab") return;
    const controls = Array.from(event.currentTarget.querySelectorAll<HTMLElement>(focusableSelector));
    const first = controls[0];
    const last = controls[controls.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last?.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first?.focus();
    }
  }

  return (
    <Dialog
      open
      fullWidth
      maxWidth="sm"
      disableRestoreFocus
      onClose={closeDialog}
      aria-labelledby="save-dialog-title"
      aria-describedby={saveRequest.error ? "save-error" : undefined}
      slotProps={{ paper: { onKeyDown: handleKeyDown } }}
    >
      <Box component="form" onSubmit={handleSave}>
        <DialogTitle id="save-dialog-title">Save to collection</DialogTitle>
        <DialogContent sx={saveRequestDialogStyles.content}>
          <TextField
            id="saved-request-title"
            label="Request title"
            value={saveRequest.title}
            disabled={saveRequest.saving}
            autoFocus
            fullWidth
            slotProps={{ htmlInput: { maxLength: 120 } }}
            onChange={event => saveRequest.setTitle(event.target.value)}
          />
          <FormControl component="fieldset" disabled={saveRequest.saving}>
            <FormLabel component="legend">Collection</FormLabel>
            <RadioGroup name="collection-choice">
              <FormControlLabel
                value="existing"
                control={
                  <Radio
                    checked={!saveRequest.createCollection}
                    disabled={history.collections.length === 0}
                    onChange={() => saveRequest.setCreateCollection(false)}
                  />
                }
                label="Choose existing"
              />
              {history.collections.length > 0 && (
                <NativeSelect
                  inputProps={{ "aria-label": "Existing collection" }}
                  value={saveRequest.collectionId}
                  disabled={saveRequest.createCollection || saveRequest.saving}
                  onChange={event => saveRequest.setCollectionId(event.target.value)}
                  sx={saveRequestDialogStyles.select}
                >
                  {history.collections.map(collection => (
                    <option key={collection.id} value={collection.id}>{collection.name}</option>
                  ))}
                </NativeSelect>
              )}
              <FormControlLabel
                value="create"
                control={
                  <Radio
                    checked={saveRequest.createCollection}
                    onChange={() => saveRequest.setCreateCollection(true)}
                  />
                }
                label="Create new collection"
              />
            </RadioGroup>
            {saveRequest.createCollection && (
              <TextField
                placeholder="Collection name"
                value={saveRequest.newCollectionName}
                required
                disabled={saveRequest.saving}
                fullWidth
                slotProps={{ htmlInput: { "aria-label": "New collection name", maxLength: 80 } }}
                onChange={event => saveRequest.setNewCollectionName(event.target.value)}
              />
            )}
          </FormControl>
          {saveRequest.error && (
            <Typography id="save-error" role="alert" color="error" variant="body2">{saveRequest.error}</Typography>
          )}
        </DialogContent>
        <DialogActions>
          <Button type="button" onClick={closeDialog} disabled={saveRequest.saving}>Cancel</Button>
          <Button type="submit" variant="contained" disabled={saveRequest.saving || !saveRequest.canSubmit}>
            {saveRequest.saving ? "Saving…" : "Save request"}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  );
});
