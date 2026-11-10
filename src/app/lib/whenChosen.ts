// A Select's change handler that ignores the empty value Radix reports by itself.
//
// Inside a form, Radix keeps a hidden native <select> in step with the chosen value.
// When that value has no option yet (the list it comes from is still loading, or no
// longer holds it), the native select falls back to "" and Radix reports "" as though
// someone had picked it. Forms here mark "none" with a sentinel item ('none'), never
// "", so a deliberate choice is never "". Since an emptied field is now sent as null,
// which clears it on the server, passing that "" on would have taken a deal's company
// or owner off on the next save without anyone choosing to.

/** `handler`, called only for a value someone chose. */
export function whenChosen(handler: (value: string) => void): (value: string) => void {
  return (value) => {
    if (value !== '') handler(value);
  };
}
