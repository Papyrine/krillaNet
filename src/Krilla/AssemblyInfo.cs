// The entire ABI is blittable by construction: every struct in Interop/ is plain-old-data and
// every string crosses as an explicit UTF-8 pointer plus length. Disabling runtime marshalling
// makes that a compile-time guarantee rather than a convention, and removes the marshalling
// stubs from every call.
[assembly: DisableRuntimeMarshalling]

// The test project verifies the ABI itself — that every Interop/ mirror agrees with the size
// the native library reports for the corresponding #[repr(C)] struct. That check has to reach
// the internal mirrors, since keeping them internal is precisely what stops the ABI leaking
// into consumers' code. The full public key is required because the assembly is strong-named
// against src/key.snk.
[assembly: InternalsVisibleTo("Krilla.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100e191859fcd1deee68b96927c170783ced0c9a471a6424a0a011cfd31156a49dd73c4ad4a88b995fb918c0b43e0c005ef5fb72d53a328a64bde825cb5f2e4c53d66f69fcbb87d6737128b98e677a42091974b5f56093123a2dd6bc738af751b101d41c4f7a996e217b61967a3aa1ae7bc791d19c1cbeef47f0cdd20d288dff1a3")]
