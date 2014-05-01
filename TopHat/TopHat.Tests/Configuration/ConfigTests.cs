﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TopHat.Configuration;
using Xunit;

namespace TopHat.Tests.Configuration
{
    public class ConfigTests
    {
        [Fact]
        public void DefaultConfigHasPluralisedTableNames()
        {
            var config = new DefaultConfiguration().Configure();

            Assert.True(config.Conventions.PluraliseNamesByDefault);
        }

        [Fact]
        public void DefaultConfigHas255StringLength()
        {
            var config = new DefaultConfiguration().Configure();

            Assert.Equal(255, config.Conventions.DefaultStringLength);
        }

        [Fact]
        public void DefaultConfigHasPrecision18()
        {
            var config = new DefaultConfiguration().Configure();

            Assert.Equal(18, config.Conventions.DefaultDecimalPrecision);
        }

        [Fact]
        public void DefaultConfigHasScale10()
        {
            var config = new DefaultConfiguration().Configure();

            Assert.Equal(10, config.Conventions.DefaultDecimalScale);
        }
    }
}