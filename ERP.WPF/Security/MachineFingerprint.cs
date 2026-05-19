using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace ERP.WPF.Security;

public static class MachineFingerprint
{
    public static string GetMachineId()
    {
        try
        {
            string cpuInfo = string.Empty;
            string volumeSerial = string.Empty;

            // 1. Pega o Serial do Processador
            using (ManagementClass mc = new ManagementClass("win32_processor"))
            {
                using (ManagementObjectCollection moc = mc.GetInstances())
                {
                    foreach (ManagementObject mo in moc)
                    {
                        cpuInfo = mo.Properties["processorID"]?.Value?.ToString() ?? "";
                        break; // Pega só o primeiro
                    }
                }
            }

            // 2. Pega o Serial do HD Principal (C:)
            using (ManagementObject dsk = new ManagementObject(@"win32_logicaldisk.deviceid=""C:"""))
            {
                dsk.Get();
                volumeSerial = dsk["VolumeSerialNumber"]?.ToString() ?? "";
            }

            // 3. Junta tudo numa string só
            string rawId = cpuInfo + volumeSerial;

            // 4. Criptografa para ficar um código bonito e padronizado (SHA256)
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawId));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("X2")); // X2 deixa em maiúsculo
                }
                
                // Retorna os primeiros 16 caracteres (Ex: 8F4E2D1A9C8B7A6D)
                return builder.ToString().Substring(0, 16);
            }
        }
        catch (Exception)
        {
            // Se der erro (ex: PC do cliente bloqueando leitura), gera um ID genérico baseado no nome do PC
            return "FALLBACK-" + Environment.MachineName.ToUpper();
        }
    }
}