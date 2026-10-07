export async function downloadFile(name, stream) {
  const data = await stream.arrayBuffer();
  const url = URL.createObjectURL(new Blob([data], { type: "text/csv;charset=utf-8" }));
  const anchor = document.createElement("a");
  anchor.href = url; anchor.download = name; anchor.click();
  URL.revokeObjectURL(url);
}
