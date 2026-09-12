package com.ciye.containerloading;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import com.unity3d.player.UnityPlayer;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;

public final class JsonPickerActivity extends Activity {
    private static final int PICK_JSON = 4107;

    public static void open(Activity owner, String receiver, String callback, String mime, String extension) {
        Intent intent = new Intent(owner, JsonPickerActivity.class);
        intent.putExtra("receiver", receiver);
        intent.putExtra("callback", callback);
        intent.putExtra("mime", mime);
        intent.putExtra("extension", extension);
        owner.startActivity(intent);
    }

    @Override protected void onCreate(Bundle state) {
        super.onCreate(state);
        if (state == null) {
            Intent picker = new Intent(Intent.ACTION_OPEN_DOCUMENT);
            picker.addCategory(Intent.CATEGORY_OPENABLE);
            String mime = getIntent().getStringExtra("mime");
            picker.setType(mime == null ? "application/json" : mime);
            picker.putExtra(Intent.EXTRA_MIME_TYPES, new String[] { mime, "application/json", "text/csv", "text/plain" });
            startActivityForResult(picker, PICK_JSON);
        }
    }

    @Override protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        String result = "";
        if (requestCode == PICK_JSON && resultCode == RESULT_OK && data != null && data.getData() != null) {
            try { result = copyToCache(data.getData()); }
            catch (Exception exception) { result = "ERROR:" + exception.getMessage(); }
        }
        UnityPlayer.UnitySendMessage(getIntent().getStringExtra("receiver"), getIntent().getStringExtra("callback"), result);
        finish();
    }

    private String copyToCache(Uri uri) throws Exception {
        String extension = getIntent().getStringExtra("extension");
        if (extension == null || !extension.matches("\\.[a-zA-Z0-9]{1,8}")) extension = ".dat";
        File output = new File(getCacheDir(), "container-import-" + System.currentTimeMillis() + extension);
        try (InputStream input = getContentResolver().openInputStream(uri); FileOutputStream stream = new FileOutputStream(output)) {
            if (input == null) throw new Exception("Không đọc được tệp đã chọn");
            byte[] buffer = new byte[8192];
            long total = 0;
            int count;
            while ((count = input.read(buffer)) != -1) {
                total += count;
                if (total > 5L * 1024L * 1024L) throw new Exception("Tệp vượt quá giới hạn 5 MB");
                stream.write(buffer, 0, count);
            }
            stream.getFD().sync();
        }
        return output.getAbsolutePath();
    }
}
