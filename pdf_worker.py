"""Local PDF tools. Requests and output files never leave this computer."""
import json
import sys
from pathlib import Path

EXPORT_FORMATS = {"jpg": ("JPEG", ".jpg"), "png": ("PNG", ".png"), "pdf-split": ("PDF", ".pdf")}
EXPORT_DPI = 300
EXPORT_MAX_PAGES = 300
EXPORT_MAX_PIXELS = 64_000_000
EXPORT_MAX_SIDE = 65_000
DAMAGED_PDF = "This PDF is damaged or incomplete and could not be read."
ENCRYPTED_PDF = "Password-protected PDFs must be unlocked before exporting pages."


def _save_raster(image, stream, fmt):
    """Write an RGB raster on a white background; JPG at quality 95, both tagged 300 DPI."""
    from PIL import Image
    converted = None
    try:
        if image.mode != "RGB":
            if image.mode in ("RGBA", "LA", "PA") or (image.mode == "P" and "transparency" in image.info):
                rgba = image.convert("RGBA")
                try:
                    converted = Image.new("RGB", rgba.size, (255, 255, 255))
                    converted.paste(rgba, mask=rgba.getchannel("A"))
                finally:
                    rgba.close()
            else:
                converted = image.convert("RGB")
        target = converted if converted is not None else image
        if fmt == "jpg":
            target.save(stream, format="JPEG", quality=95, subsampling=0, dpi=(EXPORT_DPI, EXPORT_DPI))
        else:
            target.save(stream, format="PNG", dpi=(EXPORT_DPI, EXPORT_DPI), compress_level=6)
    finally:
        if converted is not None:
            converted.close()


def _render_page(document, index, target, fmt, scale, expected):
    from PIL import Image
    page = document[index]
    bitmap = None
    image = None
    try:
        bitmap = page.render(scale=scale)
        image = bitmap.to_pil()
        width, height = image.size
        if abs(width - expected[0]) > 2 or abs(height - expected[1]) > 2 or width < 1 or height < 1 \
                or width > EXPORT_MAX_SIDE or height > EXPORT_MAX_SIDE or width * height > EXPORT_MAX_PIXELS:
            raise ValueError(f"Page {index + 1} rendered at an unexpected size.")
        with open(target, "xb") as stream:
            _save_raster(image, stream, fmt)
    finally:
        # Release the PIL view first, then the native bitmap and page, before the next page starts.
        if image is not None:
            image.close()
        if bitmap is not None:
            bitmap.close()
        page.close()
        image = bitmap = page = None
    with Image.open(target) as check:
        if check.size != (width, height) or check.format != EXPORT_FORMATS[fmt][0]:
            raise ValueError(f"Page {index + 1} could not be written correctly.")
    return {"file": target.name, "width": width, "height": height}


def _split_page(reader, index, target, metadata):
    from pypdf import PdfReader, PdfWriter
    source_page = reader.pages[index]
    rotation = int(source_page.get("/Rotate", 0)) % 360
    writer = PdfWriter()
    try:
        writer.add_page(source_page)
        if metadata:
            writer.add_metadata(metadata)
        with open(target, "xb") as stream:
            writer.write(stream)
    finally:
        writer.close()
    check = PdfReader(str(target))
    try:
        if len(check.pages) != 1 or int(check.pages[0].get("/Rotate", 0)) % 360 != rotation:
            raise ValueError(f"Page {index + 1} could not be written correctly.")
        check.pages[0].mediabox
    finally:
        check.close()
    return {"file": target.name, "rotation": rotation}


def export_pages(request):
    """Export every page of one PDF as 300 DPI JPG/PNG images or as one standalone PDF per page."""
    import math
    import os
    from pypdf import PdfReader
    import pypdfium2 as pdfium
    fmt = request.get("format")
    if not isinstance(fmt, str) or fmt not in EXPORT_FORMATS:
        raise ValueError("Choose JPG, PNG, or split PDF pages.")
    files = request.get("files")
    if not isinstance(files, list) or len(files) != 1 or not isinstance(files[0], str) or not files[0]:
        raise ValueError("Choose exactly one PDF to export.")
    output = request.get("output")
    if not isinstance(output, str) or not output:
        raise ValueError("An export folder is required.")
    output = Path(output)
    if not output.is_dir() or any(output.iterdir()):
        raise ValueError("The export folder must be a new, empty folder.")
    progress_path = request.get("progress_file")
    if progress_path is not None and (not isinstance(progress_path, str) or not progress_path):
        raise ValueError("The progress file is invalid.")
    source = Path(files[0])
    if not source.is_file():
        raise ValueError("The PDF could not be found.")
    if source.stat().st_size == 0:
        raise ValueError("This PDF is empty.")
    with open(source, "rb") as stream:
        head = stream.read(1024)
    if b"%PDF-" not in head:
        raise ValueError("This file is not a valid PDF.")
    extension = EXPORT_FORMATS[fmt][1]
    reader = None
    document = None
    progress = None
    try:
        # Validate everything (encryption, page count, every page size) before generating any file.
        try:
            reader = PdfReader(str(source))
            encrypted = reader.is_encrypted
        except Exception:
            raise ValueError(DAMAGED_PDF) from None
        if encrypted:
            raise ValueError(ENCRYPTED_PDF)
        try:
            count = len(reader.pages)
        except Exception:
            raise ValueError(DAMAGED_PDF) from None
        if count < 1:
            raise ValueError("This PDF has no pages.")
        if count > EXPORT_MAX_PAGES:
            raise ValueError(f"This PDF has {count} pages; page export supports up to {EXPORT_MAX_PAGES} pages.")
        try:
            document = pdfium.PdfDocument(str(source))
            pdfium_count = len(document)
        except Exception:
            raise ValueError(DAMAGED_PDF) from None
        if pdfium_count != count:
            raise ValueError(DAMAGED_PDF)
        # Slightly below 300/72 so exact sizes (e.g. 288 pt -> 1200 px) never round up by float error.
        scale = EXPORT_DPI / 72 * (1 - 1e-12)
        sizes = []
        scales = []
        for index in range(count):
            try:
                page = document[index]
            except Exception:
                raise ValueError(DAMAGED_PDF) from None
            try:
                width, height = page.get_size()
            finally:
                page.close()
            if not (math.isfinite(width) and math.isfinite(height)) or width <= 0 or height <= 0:
                raise ValueError(f"Page {index + 1} has invalid dimensions.")
            unit = float(reader.pages[index].get("/UserUnit", 1))
            if not math.isfinite(unit) or unit <= 0:
                raise ValueError(f"Page {index + 1} has invalid physical units.")
            page_scale = scale * unit
            pixels_w, pixels_h = math.ceil(width * page_scale), math.ceil(height * page_scale)
            if fmt != "pdf-split" and (pixels_w > EXPORT_MAX_SIDE or pixels_h > EXPORT_MAX_SIDE or pixels_w * pixels_h > EXPORT_MAX_PIXELS):
                raise ValueError(f"Page {index + 1} is too large to export at {EXPORT_DPI} DPI "
                                 f"({pixels_w} x {pixels_h} pixels; the limit is 64 megapixels per page).")
            sizes.append((max(1, pixels_w), max(1, pixels_h)))
            scales.append(page_scale)
        metadata = {}
        if fmt == "pdf-split":
            try:
                for page in reader.pages:
                    int(page.get("/Rotate", 0))
                    page.mediabox
            except Exception:
                raise ValueError(DAMAGED_PDF) from None
            try:
                metadata = {str(k): str(v) for k, v in (reader.metadata or {}).items() if isinstance(v, (str, int, float))}
            except Exception:
                metadata = {}
        if progress_path:
            progress = open(progress_path, "x", encoding="utf-8", newline="\n")
        names = []
        pages = []
        for index in range(count):
            name = f"page-{index + 1:03d}{extension}"
            target = output / name
            if fmt == "pdf-split":
                pages.append(_split_page(reader, index, target, metadata))
            else:
                pages.append(_render_page(document, index, target, fmt, scales[index], sizes[index]))
            names.append(name)
            if progress is not None:
                progress.write(json.dumps({"done": index + 1, "total": count, "file": name}) + "\n")
                progress.flush()
                os.fsync(progress.fileno())
        return {"format": fmt, "page_count": count, "files": names, "pages": pages}
    finally:
        if progress is not None:
            progress.close()
        if document is not None:
            document.close()
        if reader is not None:
            reader.close()


def main(request):
    if request.get("operation") == "export-pages":
        return export_pages(request)
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
