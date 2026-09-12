package com.ciye.containerloading;

import android.content.ContentProvider;
import android.content.ContentValues;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.provider.OpenableColumns;
import java.io.File;
import java.io.FileNotFoundException;
import java.io.IOException;

public final class PdfProvider extends ContentProvider {
    @Override public boolean onCreate() { return true; }
    @Override public String getType(Uri uri) {
        String mime = uri.getQueryParameter("mime");
        return mime == null ? "application/octet-stream" : mime;
    }
    @Override public int delete(Uri uri, String selection, String[] args) { return 0; }
    @Override public int update(Uri uri, ContentValues values, String selection, String[] args) { return 0; }
    @Override public Uri insert(Uri uri, ContentValues values) { return null; }

    @Override public ParcelFileDescriptor openFile(Uri uri, String mode) throws FileNotFoundException {
        File file = checkedFile(uri);
        return ParcelFileDescriptor.open(file, ParcelFileDescriptor.MODE_READ_ONLY);
    }

    @Override public Cursor query(Uri uri, String[] projection, String selection, String[] args, String sortOrder) {
        try {
            File file = checkedFile(uri);
            MatrixCursor cursor = new MatrixCursor(new String[] { OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE });
            cursor.addRow(new Object[] { file.getName(), file.length() });
            return cursor;
        } catch (FileNotFoundException exception) { return null; }
    }

    private File checkedFile(Uri uri) throws FileNotFoundException {
        String requested = uri.getQueryParameter("path");
        if (requested == null) throw new FileNotFoundException("Missing path");
        try {
            File file = new File(requested).getCanonicalFile();
            File internal = getContext().getFilesDir().getCanonicalFile();
            File external = getContext().getExternalFilesDir(null).getCanonicalFile();
            if ((!isInside(file, internal) && !isInside(file, external)) || !file.isFile()) throw new FileNotFoundException("Path is outside app storage");
            return file;
        } catch (IOException exception) { throw new FileNotFoundException(exception.getMessage()); }
    }

    private boolean isInside(File file, File root) {
        String path = file.getPath();
        String rootPath = root.getPath();
        return path.equals(rootPath) || path.startsWith(rootPath + File.separator);
    }
}
