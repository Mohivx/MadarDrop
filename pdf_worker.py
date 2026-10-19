"""Local PDF tools. Requests and output files never leave this computer."""
import json
import sys
from pathlib import Path

def main(request):
    from pypdf import PdfReader, PdfWriter
    op = request["operation"]
    if op == "verify":
        reader = PdfReader(request["files"][0])
        try:
            return {"page_count": len(reader.pages), "rotations": [int(page.get("/Rotate", 0)) % 360 for page in reader.pages], "metadata": {str(k).lstrip("/"): str(v) for k, v in (reader.metadata or {}).items()}}
        finally:
            reader.close()
    if op == "metadata-info":
        reader = PdfReader(request["files"][0])
        try:
            if reader.is_encrypted:
                raise ValueError("Password-protected PDFs must be unlocked before editing.")
            metadata = {str(k).lstrip("/"): str(v) for k, v in (reader.metadata or {}).items()}
            return {"metadata": metadata, "details": "\n".join(k + ": " + v for k, v in metadata.items())}
        finally:
            reader.close()
    if op.startswith("image-metadata"):
        from PIL import Image, PngImagePlugin, ExifTags
        source = request["files"][0]
        with Image.open(source) as image:
            exif = image.getexif()
            tags = {"Title": 270, "Author": 315, "Copyright": 33432, "CameraMake": 271, "CameraModel": 272, "Date": 306}
            if op == "image-metadata-info":
                values = {key: str(image.info.get(key, exif.get(tag, ""))) for key, tag in tags.items()}
                for key, tag in {"Title": 40091, "Author": 40093}.items():
                    if exif.get(tag):
                        values[key] = bytes(exif[tag]).decode("utf-16-le", errors="replace").rstrip("\x00")
                details = {str(ExifTags.TAGS.get(key, key)): str(value) for key, value in exif.items()}
                details.update({str(k): str(v)[:1500] for k, v in image.info.items() if k != "exif"})
                return {"metadata": values, "details": "\n".join(k + ": " + v for k, v in details.items())}
            remove = request.get("strip_metadata", False)
            if remove:
                exif = Image.Exif()
            values = request.get("metadata", {})
            for key, tag in tags.items():
                if values.get(key):
                    exif[tag] = values[key]
                elif tag in exif:
                    del exif[tag]
            for key, tag in {"Title": 40091, "Author": 40093}.items():
                if values.get(key):
                    exif[tag] = (values[key] + "\x00").encode("utf-16-le")
                elif tag in exif:
                    del exif[tag]
            kwargs = {}
            output_format = image.format if image.format in ("JPEG", "PNG", "TIFF", "WEBP") else "PNG"
            if output_format == "PNG":
                pnginfo = PngImagePlugin.PngInfo()
                if not remove:
                    for key, value in image.info.items():
                        if isinstance(value, str) and key not in tags:
                            pnginfo.add_text(key, value)
                for key, value in values.items():
                    if value:
                        pnginfo.add_text(key, value)
                kwargs["pnginfo"] = pnginfo
            if output_format in ("JPEG", "PNG", "TIFF", "WEBP") and len(exif):
                kwargs["exif"] = exif.tobytes()
            if not remove and image.info.get("icc_profile"):
                kwargs["icc_profile"] = image.info["icc_profile"]
            if output_format == "JPEG":
                kwargs.update(quality=95, subsampling=0)
            if getattr(image, "n_frames", 1) > 1:
                raise ValueError("Metadata editing for animated images is not supported; the original was left untouched.")
            with open(request["output"], "xb") as stream:
                image.save(stream, format=output_format, **kwargs)
            return {"saved": True}
    if op == "inspect":
        import pypdfium2 as pdfium
        folder = Path(request["folder"])
        folder.mkdir(parents=True, exist_ok=True)
        rows = []
        for source in request["files"]:
            reader = PdfReader(source)
            if reader.is_encrypted:
                raise ValueError("Password-protected PDFs must be unlocked before editing.")
            document = pdfium.PdfDocument(source)
            try:
                if len(document) + len(rows) > 300:
                    raise ValueError("This preview supports up to 300 pages in one editing session.")
                for index in range(len(document)):
                    page = document[index]
                    bitmap = None
                    try:
                        size = page.get_size()
                        bitmap = page.render(scale=min(220 / max(size), 1.0))
                        thumb = folder / (str(len(rows)) + ".png")
                        bitmap.to_pil().save(thumb)
                        rows.append({"file": source, "index": index, "rotation": 0, "thumbnail": str(thumb), "label": Path(source).name + " · " + str(index + 1)})
                    finally:
                        if bitmap is not None:
                            bitmap.close()
                        page.close()
            finally:
                document.close()
        return {"pages": rows}
    if op in ("save", "compress", "metadata"):
        writer = PdfWriter()
        readers = {}
        try:
            pages = request.get("pages")
            if pages is None:
                source = request["files"][0]
                reader = PdfReader(source)
                if reader.is_encrypted:
                    raise ValueError("Password-protected PDFs must be unlocked before editing.")
                readers[source] = reader
                pages = [{"file": source, "index": i, "rotation": 0} for i in range(len(reader.pages))]
            if not pages:
                raise ValueError("Keep at least one page in the PDF.")
            for item in pages:
                source = item["file"]
                if source not in readers:
                    readers[source] = PdfReader(source)
                    if readers[source].is_encrypted:
                        raise ValueError("Password-protected PDFs must be unlocked before editing.")
                index = int(item["index"])
                reader = readers[source]
                if index < 0 or index >= len(reader.pages):
                    raise ValueError("A selected page no longer exists.")
                page = writer.add_page(reader.pages[index])
                rotation = int(item.get("rotation", 0)) % 360
                if rotation not in (0, 90, 180, 270):
                    raise ValueError("Page rotation must be a multiple of 90 degrees.")
                if rotation:
                    page.rotate(rotation)
            if not request.get("strip_metadata", False):
                first = next(iter(readers.values()))
                if first.metadata:
                    writer.add_metadata({str(k): str(v) for k, v in first.metadata.items() if isinstance(v, (str, int, float))})
            else:
                writer.metadata = None
            metadata = request.get("metadata", {})
            if metadata:
                writer.add_metadata({"/" + k.lstrip("/"): str(v) for k, v in metadata.items()})
            if op == "compress":
                for page in writer.pages:
                    page.compress_content_streams()
                writer.compress_identical_objects(remove_identicals=True, remove_orphans=True)
            with open(request["output"], "xb") as stream:
                writer.write(stream)
            return {"page_count": len(writer.pages)}
        finally:
            writer.close()
            for reader in readers.values():
                reader.close()
    raise ValueError("Unknown PDF operation.")

if __name__ == "__main__":
    try:
        request_file, response_file = sys.argv[1:3]
        request = json.loads(Path(request_file).read_text(encoding="utf-8-sig"))
        result = main(request)
        Path(response_file).write_text(json.dumps(result, ensure_ascii=False), encoding="utf-8")
    except Exception as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
